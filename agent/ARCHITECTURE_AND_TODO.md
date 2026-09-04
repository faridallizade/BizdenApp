# Wedding Memories — Architecture Blueprint and Implementation TODO

> Status: planning only. No application code has been implemented by this document.

## 1. Product summary

This product lets an event host create an event and distribute unique QR codes. A guest scans a QR code, opens a very small mobile web page without creating an account, captures or selects a photo, and uploads it. Each QR has a server-enforced upload limit. The host sees uploaded photos in a private dashboard.

Primary priorities:

1. The guest flow must be extremely simple.
2. Original photos must not pass through the API server.
3. QR upload limits must not be bypassable.
4. QR codes must be unguessable and revocable.
5. The solution must be cheap for the MVP and scale without a rewrite.

## 2. Final recommended architecture

All frontend and backend code will live in **one Git repository** (a monorepo), but in separately deployable applications.

```text
Wedding Memories repository
│
├── apps/
│   ├── api/                  ASP.NET Core Web API
│   └── web/                  React + TypeScript + Vite frontend
│
├── src/
│   ├── WeddingMemories.Domain/
│   ├── WeddingMemories.Application/
│   └── WeddingMemories.Infrastructure/
│
├── tests/
│   ├── WeddingMemories.UnitTests/
│   └── WeddingMemories.IntegrationTests/
│
├── docs/
├── deploy/
└── agent/
```

```text
Guest / Host Browser
        │
        ├─ Frontend: React static site
        │
        ▼
ASP.NET Core API ───────── PostgreSQL
        │                    (metadata and state)
        │
        └─ presigned PUT/GET URLs
                       │
                       ▼
                Cloudflare R2 private bucket
                (original photo objects)
```

### Why this architecture

- **Single repository:** frontend, API, deployment files, contracts and documentation change together. This avoids version mismatches while keeping deployment independent.
- **Clean Architecture:** core business rules do not depend on HTTP, EF Core, PostgreSQL, R2 or React. This makes security-critical reservation logic testable and makes infrastructure replaceable.
- **Modular monolith:** one API deployment, one database and no distributed services for the MVP. It is simpler than microservices while remaining scalable because image traffic bypasses the API.
- **React + Vite:** a light public guest page and a richer dashboard can share one frontend codebase. The guest route must be lazy-loaded so dashboard code is never downloaded after QR scan.
- **Direct R2 upload:** the API creates permission and validates completion, but does not proxy 5–25 MB image files through server memory or disk.

## 3. Clean Architecture boundaries

### Domain

Pure business entities, enums and invariants. No EF Core, HTTP, R2 SDK or framework dependencies.

Examples: `Event`, `Invitation`, `Photo`, `UploadReservation`, `PhotoStatus`, `EventStatus`.

### Application

Use-cases and interfaces. This layer owns rules such as token validation, upload availability, reservation lifecycle, host ownership checks and idempotency.

Examples: `ReserveUpload`, `CompleteUpload`, `GenerateQrCodes`, `DeletePhoto`, `IObjectStorage`, `IClock`, `IInvitationRepository`.

### Infrastructure

Adapters that implement Application interfaces: EF Core/PostgreSQL repositories, R2 S3 client, password/auth services, QR/PDF generator, background job worker and structured logging.

### API

HTTP endpoints, request validation, authentication setup, authorization policies, error mapping, rate limits and dependency injection composition root.

### Web

React UI only. It calls API contracts and never decides authorization, upload limits or file validity. The backend remains the source of truth.

## 4. Main user flows

### Host

1. Host registers or signs in.
2. Host creates an event: title, date, timezone, cover, upload start/end dates.
3. Host creates QR codes with labels and per-code limits.
4. Host downloads QR PNG/SVG and later printable PDF/ZIP.
5. Host opens a private gallery, filters by QR, downloads originals or deletes photos.

### Guest

1. Guest scans `https://memories.example.com/e/{token}`.
2. The guest page validates QR status and shows event details plus remaining uploads.
3. Guest chooses **Take photo** or **Choose from gallery**.
4. Browser asks API to reserve exactly one slot.
5. API returns a short-lived R2 upload URL.
6. Browser uploads original `File` directly to R2 and displays progress.
7. Browser calls complete endpoint.
8. API verifies R2 object metadata, marks photo uploaded and returns the real remaining count.

## 5. Domain/data model

| Entity | Key fields | Rules |
|---|---|---|
| `HostUser` | Id, Name, Email, PasswordHash, IsActive, CreatedAt | Host owns events. Prefer ASP.NET Identity-backed data model. |
| `Event` | Id, PublicId, OwnerId, Name, Slug, EventDate, TimeZone, UploadStartAt, UploadEndAt, Status | All timestamps stored in UTC; IANA timezone, e.g. `Asia/Baku`, retained for display. |
| `Invitation` | Id, EventId, TokenHash, Label, UploadLimit, ReservedUploads, CompletedUploads, IsActive, ExpiresAt, LastUsedAt | One QR access credential. Token plaintext is never stored. |
| `Photo` | Id, EventId, InvitationId, StorageKey, OriginalFileName, MimeType, FileSize, Width, Height, Status, UploadedAt, DeletedAt | Database stores metadata only; no BLOB/byte array. |
| `UploadReservation` | Id, InvitationId, PhotoId, Status, ExpiresAt, CompletedAt, IdempotencyKey | Prevents race conditions and supports retries. |
| `AuditLog` | Id, ActorType, Action, EntityType, EntityId, Metadata, CreatedAt | Minimal host audit trail; tokens and signed URLs are never written here. |

Suggested statuses:

```text
EventStatus: Draft, Active, Completed, Archived
PhotoStatus: PendingUpload, Uploaded, Failed, Deleted, Quarantined, Rejected
ReservationStatus: Reserved, Completed, Expired, Cancelled
```

Important database constraints and indexes:

- unique `Invitation.TokenHash`;
- unique `(InvitationId, IdempotencyKey)` when key exists;
- indexes on `Event.OwnerId`, `Photo.EventId + CreatedAt DESC`, `Photo.InvitationId + CreatedAt DESC`, `UploadReservation.ExpiresAt`, and `UploadReservation.InvitationId + Status`;
- foreign keys from invitation/photo to event, and photo/reservation to invitation;
- ownership must always be checked from the event owner, not from an ID supplied by the client.

## 6. QR security design

QR URL format:

```text
https://memories.example.com/e/{token}
```

Token requirements:

- Generate with `.NET RandomNumberGenerator`, not `Random`.
- Use 128-bit random bytes, URL-safe Base64 encoded (about 22 characters).
- Store `SHA-256(token)` in PostgreSQL; optionally use an application secret pepper/HMAC.
- Do not log raw tokens, QR URLs, authorization cookies or presigned URLs.
- Support deactivate, regenerate and expiry.

128-bit randomness gives a search space of 2^128, which makes brute-force guessing infeasible. Larger tokens add little practical security here while making the printed QR more complex.

A QR screenshot is equivalent to sharing a bearer credential. It can use the same QR limit from another device. The host can revoke it and generate a replacement; the system cannot identify a guest without adding login, which is intentionally outside MVP scope.

Print guidance:

- Use a short stable domain.
- Use QR error correction level M by default, Q if cards may be damaged.
- Minimum printed QR size: 25–30 mm; test with target phone cameras before large printing.
- Add a short instruction beside it: “Scan. Capture. Share the memory.”

## 7. Upload reservation, concurrency and idempotency

The frontend may display remaining uploads, but it cannot enforce them. Browser storage, cookies, device IDs and JavaScript can all be changed or bypassed.

Available uploads are calculated as:

```text
UploadLimit - CompletedUploads - ReservedUploads
```

### Reserve algorithm

Inside one PostgreSQL transaction:

1. Validate the token, QR status, event status and upload window.
2. Release any expired reservations for this invitation.
3. Run an atomic conditional update:

```text
UPDATE invitations
SET reserved_uploads = reserved_uploads + 1
WHERE id = @invitationId
  AND completed_uploads + reserved_uploads < upload_limit;
```

4. If no row changes, return `UPLOAD_LIMIT_REACHED`.
5. Create `Photo` with `PendingUpload` status and a `Reserved` reservation expiring in 10 minutes.
6. Commit and create the presigned R2 URL for that exact storage key.

This prevents five parallel requests from claiming five slots when only one remains. A concurrency integration test must issue 100 simultaneous requests against a limit of 15 and prove exactly 15 succeed.

### Complete algorithm

1. Validate QR token and confirm the photo belongs to that invitation.
2. Find reservation and lock/update it transactionally.
3. If it is already `Completed`, return idempotent success instead of incrementing counters again.
4. If expired, return `UPLOAD_RESERVATION_EXPIRED`.
5. Check R2 object existence and expected metadata.
6. Mark reservation completed, mark photo uploaded, decrement `ReservedUploads`, increment `CompletedUploads` in one transaction.

### Reservation expiry and reconciliation

A `BackgroundService` every 1–5 minutes is sufficient for MVP:

- expire stale reservations and decrement their reserved counter;
- remove stale R2 objects belonging to expired/failed reservations;
- detect an R2 object uploaded without complete call and flag it for cleanup/reconciliation.

No Redis, Hangfire, Kafka or message queue is needed initially. Persistent background job infrastructure becomes useful only after there are multiple expensive/retryable workflows such as thumbnails, archive generation and email reminders.

## 8. R2 storage lifecycle and validation

Storage key is generated only by the backend:

```text
events/{eventId}/photos/{yyyy}/{mm}/{photoUuid}
```

The original filename is metadata only. It must never be used in an object key.

### Allowed uploads

- JPEG
- PNG
- WebP
- HEIC/HEIF
- Initial maximum size: 25 MB
- Explicitly reject SVG because it may contain executable content.

Client-side MIME type validation improves UX but is not security. During completion, API checks R2 HEAD metadata for file size and content type and, where practical, reads the initial bytes to validate the image signature. Uploads that fail validation are marked failed and deleted from storage.

### Privacy/access

Use a **private R2 bucket**:

- Guests obtain only short-lived signed PUT access to one generated key.
- Hosts obtain signed GET/download URLs only after owner authorization.
- Guests cannot list or read other guests’ photos.
- Never expose public bucket/CDN object URLs for private event media.

Original quality is retained by uploading the browser `File` directly. Do not canvas resize or JPEG recompress in MVP.

## 9. Guest mobile UX

The public page must be mobile-first and small:

```text
Event cover
“Nigar & Orxan”
“Bu gecənin xatirəsini bizimlə paylaş ❤️”
11 / 15 şəkil haqqı qalıb
[ Şəkil çək ]
[ Qalereyadan seç ]
```

Use native file inputs, not a custom camera UI:

```html
<input type="file" accept="image/*" capture="environment">
```

`capture` is a browser hint, not a guarantee. iPhone Safari may offer Camera/Photo Library/Browse while Android Chrome often opens the camera. This is still much more reliable for MVP than a custom `getUserMedia` camera experience.

UX requirements:

- separate capture and gallery choices when device/browser permits;
- display upload progress via XHR or equivalent upload-progress API;
- disable duplicate click while a reservation/upload is active;
- retain selected file in memory to allow manual retry after network error;
- do not silently auto-retry repeatedly on mobile data; one safe automatic retry for transient network error, then a visible **Retry** button;
- refresh remaining count from server after completion;
- a clear end state when the limit is reached.

Full offline upload is not MVP. A later PWA/IndexedDB queue can persist pending images and retry when connectivity returns, but it has storage quotas, browser eviction and privacy implications.

## 10. Host dashboard UX

Screens:

1. Event list: event name, date, status, photo count and QR count.
2. Event settings: title, cover, upload window, timezone, activate/deactivate.
3. QR management: label, limit, completed/reserved/remaining, deactivate, regenerate, download.
4. Gallery: paginated thumbnail grid, source QR filter, date sort, preview, original download, delete.

Do not load 5–25 MB originals for every gallery tile. For the first MVP this can use signed URLs with browser-rendered originals only for a limited page size, but a thumbnail/preview job should be the first post-MVP performance feature. The `Photo` model should therefore reserve optional `ThumbnailStorageKey` and `PreviewStorageKey` fields.

Deletion recommendation:

1. Host authorizes deletion.
2. API soft-deletes row by setting `DeletedAt` and hides it immediately from gallery.
3. Background worker deletes the R2 object with retry.
4. Object deletion failures remain observable and retryable.

## 11. API inventory

| Method | Route | Authentication | Purpose |
|---|---|---|---|
| GET | `/api/public/events/{token}` | QR token | Validate QR and return public event data/remaining count |
| POST | `/api/public/uploads/reserve` | QR token | Reserve slot and return signed R2 PUT URL |
| POST | `/api/public/uploads/{photoId}/complete` | QR token | Verify R2 object and finalize upload |
| POST | `/api/host/auth/login` | Public | Host sign-in |
| POST | `/api/host/auth/logout` | Host cookie | Sign out |
| GET/POST | `/api/host/events` | Host | Event list/create |
| GET/PATCH | `/api/host/events/{eventId}` | Owner | Read/update event |
| POST | `/api/host/events/{eventId}/qrs/generate` | Owner | Generate QR batch |
| GET | `/api/host/events/{eventId}/qrs` | Owner | List QR codes |
| PATCH | `/api/host/qrs/{id}` | Owner | Change label/limit/status |
| POST | `/api/host/qrs/{id}/regenerate` | Owner | Revoke token and create new QR |
| GET | `/api/host/events/{eventId}/photos` | Owner | Paginated gallery |
| GET | `/api/host/photos/{photoId}/download` | Owner | Signed original download URL |
| DELETE | `/api/host/photos/{photoId}` | Owner | Soft delete request |

Shared error format:

```json
{
  "code": "UPLOAD_LIMIT_REACHED",
  "message": "Bu QR kod üçün şəkil limiti bitib.",
  "requestId": "..."
}
```

Primary codes: `INVALID_QR`, `QR_INACTIVE`, `QR_EXPIRED`, `EVENT_NOT_ACTIVE`, `UPLOAD_WINDOW_NOT_OPEN`, `UPLOAD_WINDOW_CLOSED`, `UPLOAD_LIMIT_REACHED`, `INVALID_FILE_TYPE`, `FILE_TOO_LARGE`, `UPLOAD_RESERVATION_EXPIRED`, `UPLOAD_ALREADY_COMPLETED`, `RATE_LIMITED`, `FORBIDDEN`, `STORAGE_VALIDATION_FAILED`.

## 12. Authentication, CORS and web security

### Host auth

Use ASP.NET Identity with secure HttpOnly cookies. The cookie is inaccessible to JavaScript, reducing token theft risk from XSS. Set `Secure`, `HttpOnly`, appropriate `SameSite`, and anti-forgery protection for state-changing host requests.

### Guest auth

Guest has no account. QR token is a restricted bearer credential, accepted only by public event/upload endpoints.

### CORS

If frontend and API are separate origins, allow only named frontend origins such as `https://memories.example.com`. Never use wildcard CORS for host authenticated endpoints. Configure R2 CORS to permit only the frontend origin, `PUT` for required upload headers and `GET/HEAD` only where required.

### Rate limits

- Host login: strict IP + account identifier limit.
- Public QR lookup: token + IP limit.
- Reserve and complete: token + IP limit.
- IP-only limits are insufficient because mobile carriers often place many users behind one NAT IP.

### Logging and observability

Use structured logging with `RequestId`, `EventId`, `InvitationId`, `PhotoId` and outcome. Never log password, cookie, raw token or full signed URL.

Minimum metrics:

- API request count and error rate;
- upload reservations/completions/failures;
- expired reservations;
- R2 failures;
- average upload size;
- active event count.

## 13. Deployment and configuration

Recommended MVP deployment:

```text
web     → Cloudflare Pages
api     → Docker container on VPS
db      → Managed PostgreSQL (Neon, Supabase, or another provider)
media   → Cloudflare R2 private bucket
domain  → Cloudflare DNS + HTTPS
```

All secrets must be outside source code:

- local: .NET user-secrets and local `.env` not committed to Git;
- production: platform environment variables/secret store;
- CI/CD: repository/hosting secret manager.

Required secrets:

```text
ConnectionStrings__Postgres
R2__AccountId
R2__AccessKeyId
R2__SecretAccessKey
R2__BucketName
R2__Endpoint
Auth__CookieOrDataProtectionConfiguration
TokenHashPepper
```

## 14. Your responsibilities: external setup checklist

These are account, billing, domain and deployment actions that require your control. I can provide exact values/configuration templates when you reach each implementation phase.

- [ ] Buy or select a stable domain, for example `memories.example.com`.
- [ ] Create or use an existing Cloudflare account.
- [ ] Add the domain to Cloudflare and update registrar nameservers.
- [ ] Create an R2 bucket, for example `wedding-memories-production`.
- [ ] Create an R2 API token/access key with the minimum bucket-level read/write permission required by API.
- [ ] Copy R2 Account ID, S3 endpoint, Access Key ID and Secret Access Key into a password manager; never commit them.
- [ ] Configure R2 bucket CORS after frontend origin is final.
- [ ] Choose a managed PostgreSQL provider and create separate development/staging/production databases.
- [ ] Save PostgreSQL connection strings as secrets, not repository files.
- [ ] Choose API hosting: recommended small VPS with Docker; create server and SSH access.
- [ ] Set up Cloudflare DNS records for `memories.example.com` and `api.memories.example.com`.
- [ ] Set up production secret variables in API host and frontend host.
- [ ] Create a Git repository and choose CI/CD identity/provider access.
- [ ] Decide whether host registration is public or hosts are manually created by an administrator.
- [ ] Decide image retention policy: permanent, archive after N months, or automatic deletion after N months.

## 15. Implementation TODO — phase by phase

### Phase 0 — Confirm product decisions

**Goal:** remove decisions that would otherwise cause rework.

- [ ] Confirm domain and production environment names.
- [ ] Confirm open registration vs admin-created host accounts.
- [ ] Confirm image retention period.
- [ ] Confirm initial UI language(s).
- [ ] Confirm maximum image size: recommended 25 MB.
- [ ] Confirm whether QR PDF/ZIP is MVP or immediately after MVP.
- [ ] Confirm whether HEIC preview is required in MVP; original download will be supported regardless.
- [ ] Confirm Cloudflare R2, managed PostgreSQL and hosting provider.

**Done when:** decisions are recorded in repository documentation and secrets exist only in proper secret stores.

### Phase 1 — Monorepo and Clean Architecture skeleton

**Backend work:** create .NET solution, Domain/Application/Infrastructure/API projects, dependency rules and health endpoint.

**Frontend work:** create React/Vite application with guest/dashboard route boundaries and API client foundation.

**Your work:** create Git repository and provide no credentials in chat; put them into the chosen secret manager when requested.

**Security/tests:** ensure `.gitignore`, example environment file without values, build/lint/test commands and CI base workflow.

**Done when:** API and web build independently from the same repository; no secret is tracked.

### Phase 2 — Database and domain model

**Backend work:** entities, EF Core mappings, migrations, indexes, constraints, UTC/timezone handling and seed-free local setup.

**Frontend work:** none beyond typed API contract placeholders.

**Your work:** create development PostgreSQL database or approve Docker-local PostgreSQL for development.

**Security/tests:** migration test, ownership/query index verification and timestamp tests.

**Done when:** schema represents Event, Invitation, Photo and Reservation correctly, and migration applies to an empty database.

### Phase 3 — Host authentication and authorization

**Backend work:** ASP.NET Identity, cookie sign-in/out, password policy, owner authorization policy, rate limit and anti-forgery design.

**Frontend work:** login page, logout and protected dashboard routing.

**Your work:** decide account onboarding model.

**Security/tests:** unauthorized cross-host event access test, CSRF checks and brute-force rate-limit test.

**Done when:** host can sign in and only access their own resources.

### Phase 4 — Event CRUD and upload window

**Backend work:** host event create/read/update, status transition and upload window validation.

**Frontend work:** event list, create event form and event settings.

**Your work:** define event content/copy and preferred timezone defaults.

**Security/tests:** verify inactive events and closed windows reject public uploads.

**Done when:** host fully manages events and timezone-aware upload acceptance works.

### Phase 5 — QR generation and management

**Backend work:** secure token creation/hash, QR batch generation, label/limit editing, deactivate/regenerate and image export.

**Frontend work:** QR creation form and QR table with usage states.

**Your work:** decide print layout and language; test sample QR physically with phones.

**Security/tests:** prove raw token is absent from DB/logs; regenerate immediately invalidates prior token.

**Done when:** host can generate/download/test/revoke QR codes.

### Phase 6 — Public QR page

**Backend work:** public QR lookup with state/expiry/window checks and public-safe response.

**Frontend work:** ultra-light guest page, event cover, remaining count and error states.

**Your work:** approve guest-facing design/copy.

**Security/tests:** invalid/inactive/expired token tests and public rate limits.

**Done when:** valid QR opens guest page and all invalid states are safe and understandable.

### Phase 7 — Reservation engine

**Backend work:** atomic reserve transaction, reservation expiry, idempotency key, complete state machine and cleanup worker.

**Frontend work:** file selection state, duplicate-click prevention and server-driven remaining count.

**Your work:** none except testing on target mobile devices when available.

**Security/tests:** 100 parallel reserve requests against limit 15; exactly 15 successes. Test replayed completion and expired reservation.

**Done when:** limit cannot be exceeded under concurrency and abandoned uploads release slots.

### Phase 8 — Cloudflare R2 integration

**Backend work:** S3-compatible R2 client, scoped presigned PUT/GET URLs, completion HEAD validation, file signature/size validation and orphan cleanup.

**Frontend work:** direct upload progress, retry flow and failure messages.

**Your work:** create R2 bucket/access key/CORS configuration and provide configuration through secret store.

**Security/tests:** private bucket proof, upload type/size tests, signed URL expiry test.

**Done when:** browser uploads directly to private R2 and API never carries image binary data.

### Phase 9 — Guest mobile polish

**Backend work:** only support endpoints if UX needs them.

**Frontend work:** native camera/gallery inputs, progress UI, low-bandwidth UX, retry and accessible states.

**Your work:** test physical iPhone Safari, Android Chrome and Samsung Internet.

**Security/tests:** camera/gallery/HEIC/large-file/network interruption matrix.

**Done when:** QR scan to completed upload works reliably on supported phones.

### Phase 10 — Host gallery and photo lifecycle

**Backend work:** paginated gallery, QR/date filter, signed download URL, authorized soft delete and R2 deletion worker.

**Frontend work:** responsive grid, preview/detail screen, delete confirmation and filters.

**Your work:** approve gallery design and confirm initial page size.

**Security/tests:** host isolation, deleted-media visibility and deletion retry tests.

**Done when:** hosts safely browse, download and delete only their event photos.

### Phase 11 — Security and operations hardening

**Backend work:** explicit CORS, CSP/security headers, full rate-limit policy, structured logs, metrics and health/readiness endpoints.

**Frontend work:** error-state consistency and no secret leakage in browser bundle.

**Your work:** configure production origins, monitoring destination and secret values.

**Security/tests:** security review checklist and production configuration smoke test.

**Done when:** logs/metrics exist, production endpoints are hardened, and secrets are validated outside Git.

### Phase 12 — Deployment and launch

**Backend work:** Dockerfile, deployment config, migrations-at-deploy strategy and runbook.

**Frontend work:** production build and Pages deployment config.

**Your work:** provision VPS/database/domain/DNS/Cloudflare and approve production deployment.

**Security/tests:** staging environment smoke test, HTTPS, backup restore verification and sample QR end-to-end upload.

**Done when:** a printed production QR completes a real upload and host dashboard sees it.

## 16. Cost and scaling notes

With 5 MB average photos:

| Event size | Photos | New storage |
|---|---:|---:|
| Small wedding | 800 | 4 GB |
| Medium wedding | 2,000 | 10 GB |
| Large wedding | 5,000 | 25 GB |

Cloudflare R2 Standard currently includes 10 GB-month storage, 1 million Class A operations and 10 million Class B operations each month. After free usage, Standard storage is $0.015/GB-month; direct egress is free. This makes the first material costs more likely to be PostgreSQL and API hosting than R2. Verify live pricing before launch: [Cloudflare R2 pricing](https://developers.cloudflare.com/r2/pricing/).

The API scales well because photos bypass it. At 10–1,000 events, PostgreSQL query/index quality, signed URL generation and thumbnail strategy matter more than raw API bandwidth. At much higher scale, independently scale API replicas and add thumbnail/archive workers; the Domain/Application contracts should remain unchanged.

## 17. Explicit non-goals for initial MVP

- No AI moderation, NSFW classification or face recognition.
- No public guest photo gallery.
- No microservices, Kafka, RabbitMQ, Redis, Kubernetes or CQRS framework.
- No image binary in PostgreSQL.
- No guest login, OTP or mobile application installation.
- No offline upload queue.
- No full archive/download-all job until demand requires it.

