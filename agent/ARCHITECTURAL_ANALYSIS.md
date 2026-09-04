# Wedding Memories — Architectural Analysis

This document stores the architectural blueprint supplied for the Wedding Memories MVP. The implementation checklist and external setup responsibilities are maintained in [ARCHITECTURE_AND_TODO.md](ARCHITECTURE_AND_TODO.md).

## 1. Product understanding

An event host creates an event and unique QR codes. Guests scan a QR code and upload original photos from a mobile browser without an account. Every QR has a server-enforced limit. Hosts alone manage uploaded photos in a private dashboard.

- QR is a bearer credential: anyone holding it can consume its shared limit.
- Image binary data goes directly to R2, not through the API server.
- The MVP is a modular monolith; no microservices, Redis, queues or Kubernetes.

## 2. User flows

**Host:** login → create event → define upload window → generate/print QR codes → monitor usage → browse, download or delete private photos.

**Guest:** QR scan → public event page → take/select photo → reserve one upload → direct R2 upload → complete confirmation → real remaining limit from API.

There is no guest gallery or guest “View Event Photos” flow.

## 3. System architecture

```text
Guest / Host browser → React frontend → ASP.NET Core API → PostgreSQL
                                         ↓
                                signed R2 upload/download URL
                                         ↓
                               private Cloudflare R2 bucket
```

The API controls authorization, tokens, limits, reservation state and metadata. R2 stores image objects. PostgreSQL stores no image BLOBs.

## 4. Backend architecture

Use .NET 8 LTS, ASP.NET Core Web API, EF Core, PostgreSQL and an S3-compatible R2 SDK.

```text
apps/api                         HTTP composition root
src/WeddingMemories.Domain       entities and business rules
src/WeddingMemories.Application  use-cases and interfaces
src/WeddingMemories.Infrastructure EF Core, R2, auth, QR adapters
tests/                           unit and integration tests
```

This Clean Architecture structure keeps domain rules independent of HTTP, database and R2 details.

## 5. Frontend architecture

Use React, TypeScript and Vite in the same repository as the backend. Deploy the static frontend separately from the API.

Routes:

```text
/e/:token                 guest public upload page
/login                    host login
/dashboard                event list
/dashboard/events/:id     event, QR management and host-only gallery
```

The guest route is lazy-loaded and must not download dashboard code.

## 6. Domain model

| Entity | Responsibility |
|---|---|
| `HostUser` | authenticated event owner |
| `Event` | event configuration, timezone, upload window and status |
| `Invitation` | one QR credential, token hash and counters/limit |
| `Photo` | image metadata and R2 storage key |
| `UploadReservation` | temporary slot and idempotent upload lifecycle |
| `AuditLog` | minimum host action history |

Use UUID primary keys. Store times in UTC and retain an IANA timezone such as `Asia/Baku` for event display.

Important indexes: unique `Invitation.TokenHash`; `Event.OwnerId`; `Photo.EventId, CreatedAt`; `Photo.InvitationId, CreatedAt`; `UploadReservation.ExpiresAt`; and `UploadReservation.InvitationId, Status`.

## 7. QR security design

QR URL format:

```text
https://memories.example.com/e/{token}
```

Generate a 128-bit token with `.NET RandomNumberGenerator`, encode it with URL-safe Base64 and store only `SHA-256(token)` (optionally HMAC-peppered) in the database. Do not store or log the plaintext token.

QRs support expiry, deactivate and regenerate. A QR screenshot shares its bearer access; revocation/regeneration is the mitigation.

## 8. Upload limit and concurrency

The backend alone controls:

```text
available = UploadLimit - CompletedUploads - ReservedUploads
```

Reservation uses one PostgreSQL transaction and an atomic conditional update:

```sql
UPDATE invitations
SET reserved_uploads = reserved_uploads + 1
WHERE id = @invitationId
  AND completed_uploads + reserved_uploads < upload_limit;
```

If no row is updated, the limit has been reached. Then create `Photo(PendingUpload)` and `UploadReservation(Reserved)` with a 10-minute expiry. On completion, mark the reservation completed and atomically decrement reserved/increment completed. A repeated complete call returns idempotent success and never increments twice.

## 9. R2 upload lifecycle

1. `POST /api/public/uploads/reserve` validates QR/event/window/file metadata and reserves one slot.
2. API returns `photoId`, storage key and short-lived signed R2 PUT URL.
3. Browser uploads the original `File` directly to R2 with visible progress.
4. `POST /api/public/uploads/{photoId}/complete` validates the matching reservation and checks R2 object metadata.
5. API marks the photo uploaded and returns actual remaining capacity.

Expired reservations release their slot. A simple `BackgroundService` cleans stale reservations and orphan objects. This is enough for MVP.

## 10. Host authentication

Use ASP.NET Identity with secure HttpOnly cookies for the web dashboard. Configure `Secure`, `HttpOnly`, `SameSite`, CSRF protection and strict login rate limiting. JWT is unnecessary until a native/mobile third-party client exists.

## 11. Guest browser/mobile UX

Use native browser picker/camera behavior:

```html
<input type="file" accept="image/*" capture="environment">
```

This is more reliable than custom `getUserMedia` camera UI on iPhone Safari and Android Chrome. Guest UI has event intro, capture/gallery buttons, progress, retry, success and limit-reached states. It does not have a gallery.

## 12. Host dashboard UX

Host screens include event list, event settings, QR management, gallery, photo view and settings. Gallery uses pagination, QR/date filtering, signed original download and soft-delete workflow.

Original 5–25 MB files should not be loaded in every grid card. Thumbnail/preview generation is a prioritized post-MVP addition; reserve metadata fields now for thumbnail and preview keys.

## 13. File and storage security

R2 bucket remains private. Guests receive a short-lived PUT URL for exactly one generated object key. Hosts receive short-lived GET URLs after ownership authorization.

Allowed types: JPEG, PNG, WebP and HEIC/HEIF. Start with 25 MB maximum. Reject SVG. Never use an original filename as an object path; use a backend-generated key such as:

```text
events/{eventId}/photos/{yyyy}/{mm}/{photoUuid}
```

Completion verifies R2 existence, size, content type and, where practical, image signature bytes.

## 14. API design

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/api/public/events/{token}` | QR | public event info/remaining quota |
| POST | `/api/public/uploads/reserve` | QR | reserve a slot and issue PUT URL |
| POST | `/api/public/uploads/{photoId}/complete` | QR | validate and finalize upload |
| POST | `/api/host/auth/login` | public | host sign in |
| POST | `/api/host/auth/logout` | host | sign out |
| GET/POST | `/api/host/events` | host | list/create events |
| GET/PATCH | `/api/host/events/{eventId}` | owner | read/update event |
| POST | `/api/host/events/{eventId}/qrs/generate` | owner | create QR batch |
| GET | `/api/host/events/{eventId}/qrs` | owner | list QR codes |
| PATCH | `/api/host/qrs/{id}` | owner | update QR |
| POST | `/api/host/qrs/{id}/regenerate` | owner | revoke and replace QR |
| GET | `/api/host/events/{eventId}/photos` | owner | paginated gallery |
| GET | `/api/host/photos/{photoId}/download` | owner | signed download |
| DELETE | `/api/host/photos/{photoId}` | owner | soft delete |

## 15. Error handling

Every API failure uses a stable code, human message and request ID:

```json
{ "code": "UPLOAD_LIMIT_REACHED", "message": "Bu QR kod üçün şəkil limiti bitib.", "requestId": "..." }
```

Typical codes: `INVALID_QR`, `QR_INACTIVE`, `QR_EXPIRED`, `EVENT_NOT_ACTIVE`, `UPLOAD_WINDOW_CLOSED`, `UPLOAD_LIMIT_REACHED`, `INVALID_FILE_TYPE`, `FILE_TOO_LARGE`, `UPLOAD_RESERVATION_EXPIRED`, `UPLOAD_ALREADY_COMPLETED`, `RATE_LIMITED`, `FORBIDDEN` and `STORAGE_VALIDATION_FAILED`.

## 16. Threat model

| Threat | Mitigation |
|---|---|
| QR guessing/enumeration | 128-bit secure random token, token hash, rate limit |
| Direct API/frontend bypass | all business decisions server-side |
| Parallel uploads | atomic conditional database update |
| Replay | reservation status and idempotency |
| Signed URL theft/expiry | short TTL and single scoped key |
| MIME spoofing/oversize | completion-time R2 validation |
| XSS/CSRF/CORS | output escaping/CSP, anti-forgery, explicit origin allow-list |
| Unauthorized media read | private bucket and owner-authorized signed GET |
| DoS | token + IP limits; not IP-only due to carrier NAT |

## 17. Deployment architecture

Recommended MVP topology:

```text
React static frontend → Cloudflare Pages
ASP.NET API            → Docker on a small VPS
PostgreSQL             → managed PostgreSQL
Media                  → private Cloudflare R2
```

Use a stable domain and HTTPS. QR URLs must remain valid after printing. Secrets go in user-secrets/environment variables/secret stores, never source files.

## 18. Cost estimate

At 5 MB per image: 800 photos = 4 GB, 2,000 = 10 GB, 5,000 = 25 GB. R2 Standard includes 10 GB-month storage, 1 million Class A and 10 million Class B operations; after included usage storage is currently $0.015/GB-month and egress is free. Verify before launch: [Cloudflare R2 pricing](https://developers.cloudflare.com/r2/pricing/).

Early operating cost is primarily VPS and managed PostgreSQL, commonly around $5–15/month and $0–25/month respectively depending on provider.

## 19. Testing strategy

- Unit tests for token/window/state rules.
- PostgreSQL integration tests for migrations and transactions.
- Concurrency test: 100 parallel reservations with limit 15 must produce exactly 15 successes.
- Replay, expired reservation, invalid token, inactive event and wrong QR/photo tests.
- Mobile matrix: iPhone Safari, Android Chrome, Samsung Internet, desktop fallback, HEIC, large images and weak-network retry.

## 20. MVP scope

Included: host auth, event CRUD, QR generation/revocation, upload windows, per-QR limits, direct R2 upload, guest mobile upload UX, host-only gallery/download/delete, logging and basic monitoring.

Excluded: AI moderation, recognition, guest gallery, album/favorites, offline queue, full download-all archive, billing and multi-host roles.

## 21. Roadmap

1. Thumbnail/preview pipeline including HEIC previews.
2. Background ZIP archive generation.
3. Retention/archival reminders and deletion policy.
4. Offline IndexedDB queue.
5. Moderation status pipeline.
6. Host teams, analytics, backups/versioning.

## 22. Implementation phases

0. Confirm product/hosting/retention decisions.
1. Build single-repository Clean Architecture skeleton.
2. Add domain model and PostgreSQL migrations.
3. Implement host authentication and authorization.
4. Implement event CRUD and upload windows.
5. Implement secure QR generation and management.
6. Implement public QR validation page.
7. Implement reservations, expiry and concurrency tests.
8. Integrate private R2 direct upload.
9. Polish guest mobile UX.
10. Implement host gallery and deletion lifecycle.
11. Security/observability hardening.
12. Deploy to staging/production.

Each phase requires an explicit `Tətbiq et: Phase N` request before code is written.

## 23. Open technical decisions

- Is host signup public or administrator-created?
- What is the retention duration?
- Is a QR assigned per table, guest or family?
- Are PDF/ZIP QR exports in first MVP?
- Is HEIC preview required immediately?
- Which guest languages ship first?
- Are Cloudflare account and domain already available?
- Will event cover be uploaded or initially template-based?

## 24. Final recommendation

Use one repository containing React/Vite frontend and .NET 8 Clean Architecture backend; PostgreSQL for metadata; private R2 for originals; 128-bit random hashed QR tokens; atomic PostgreSQL reservation counters; ASP.NET Identity cookies for hosts; and Cloudflare Pages + Docker VPS + managed PostgreSQL for deployment. This meets the guest simplicity, privacy, cost and scale priorities without premature infrastructure.
