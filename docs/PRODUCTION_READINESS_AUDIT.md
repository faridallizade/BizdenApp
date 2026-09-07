# Bizdən — deployment readiness audit

Audit date: 2026-09-07

## Current deployment status

The Docker stack is suitable for **local and private staging** on the Windows laptop. The web container is the only host-facing service. PostgreSQL and the .NET API remain on the internal Docker network, which removes the port conflicts seen on `55432` and `55080`.

This is **not yet public-production ready**. Do not publish QR links to guests until the public-blocker items below are completed.

## Current Windows port issue

### Symptom

Docker reported `ports are not available` for `55173` after earlier failures for `55432` and `55080`.

### Cause

Windows, WSL2, Hyper-V, a VPN, or another application can reserve a TCP port. Docker cannot bind a reserved port even if there is no visible normal process using it.

### Implemented fix

Only the web service opens a host port. It is now configurable through `.env`:

```env
WEB_PORT=58081
```

If `58081` is unavailable, change only this value to `58082`, run `docker compose down`, then run `docker compose up --build -d`. API and PostgreSQL do not need host ports.

## Fixed deployment risks

- [x] API calls are same-origin through Nginx `/api` proxy. A phone no longer tries to call its own `localhost:55080`.
- [x] PostgreSQL host port mapping removed.
- [x] API host port mapping removed.
- [x] Web host port is configurable through `WEB_PORT`.
- [x] PostgreSQL data uses a named Docker volume.
- [x] ASP.NET Data Protection keys use a named Docker volume. Host login cookies now survive API container recreation.

## Public blockers — complete before sharing QR links

### 1. HTTPS public access

**Risk:** The Windows laptop currently serves HTTP only. Host login credentials and session cookies must not travel over public HTTP.

**Required:** Create a Cloudflare Tunnel from this laptop to the web container and map a real domain, for example `app.example.az`, to `http://localhost:<WEB_PORT>`. Use Cloudflare-managed HTTPS. Do not configure router port forwarding.

**Why:** Tunnel creates an outbound encrypted connection; no public inbound port needs to be opened on the office router.

### 2. Production environment and proxy headers

**Risk:** Compose currently uses `ASPNETCORE_ENVIRONMENT=Development`. That permits non-secure local cookies and is unsuitable for an internet-facing service.

**Required:** Add a production compose profile/configuration before enabling the public domain. Set `ASPNETCORE_ENVIRONMENT=Production`, configure trusted forwarded headers, and confirm cookies are marked Secure after the tunnel is live.

**Why:** The app needs to understand that the visitor used HTTPS at Cloudflare even though the local Nginx-to-API hop is HTTP.

### 3. R2 CORS origin

**Risk:** R2 accepts browser PUT uploads only from explicitly allowed origins. A wrong origin creates browser CORS errors even though API login works.

**Required now for local test:** add `http://localhost:58081` to R2 CORS. If `WEB_PORT` changes, update that origin too.

**Required for public:** add only the final `https://your-domain` origin. Do not use `*` because browser uploads are permission-sensitive.

### 4. Rotate learning credentials

**Risk:** The current R2 credentials have been exposed during setup and should be treated as development-only.

**Required:** Before public release, delete that token in Cloudflare, create a new token restricted to `bizden-media` with Object Read & Write, and replace the local `.env` values. Never commit `.env`.

### 5. Guest endpoint abuse controls

**Risk:** Host authentication is rate-limited, but public QR reservation/upload endpoints still need their own IP and QR-token rate limits. Otherwise automated requests can consume resources and signed-upload generation capacity.

**Required:** Implement public endpoint rate limits, request-size limits, structured audit logs, and alerting before public release.

### 6. File-content verification

**Risk:** The server validates file size and declared MIME type, but an attacker can label non-image bytes as `image/jpeg`.

**Required:** Add background image inspection: decode the uploaded file, reject invalid files, strip EXIF metadata where needed, generate thumbnails, and quarantine failed objects.

**Why:** R2 is private, but the application must still not treat a claimed content type as proof of a safe image.

### 7. Gallery and download authorization

**Risk:** Host gallery/photo-management screens in the visual design are not yet fully implemented as a secure, production gallery flow.

**Required:** Implement host-only gallery listing, short-lived signed GET URLs, delete/moderation workflow, pagination, and authorization tests before exposing any photo gallery.

### 8. Backup and recovery

**Risk:** Docker volumes persist across normal restarts but are not a backup. Disk failure, Windows reinstall, or accidental `docker compose down --volumes` can lose metadata.

**Required:** Schedule encrypted PostgreSQL backups to a separate location and document one restore test. R2 objects remain separate from database records, so both must be recoverable.

### 9. Laptop server reliability

**Risk:** Sleep, power loss, Windows updates, unstable Wi-Fi, or AnyDesk disconnection can interrupt event uploads.

**Required:** Connect the laptop by Ethernet if possible, keep it on power, disable sleep while plugged in, verify automatic reboot policy, and keep a backup hotspot/internet option for events.

## Functional issue found in the current UI

The host event form presents a `Bağlı` status with value `Closed`, while the backend enum uses `Completed` and `Archived`. Selecting `Bağlı` can make event save fail with an invalid enum value.

**Required:** Align the frontend status values and Azerbaijani labels with backend enum values before host event creation is tested broadly.

## Validation checklist for this staging server

- [ ] `docker info` shows a valid Server section.
- [ ] `docker compose ps` shows `bizden-web`, `bizden-api`, and `bizden-postgres` running. `bizden-migrate-1` exited successfully is expected.
- [ ] `http://localhost:<WEB_PORT>/health` returns `Healthy`.
- [ ] Host registration and login works after browser refresh.
- [ ] Create an Active event with an upload window that includes the current time.
- [ ] Generate a QR code and open it on the same Windows browser.
- [ ] Upload a small JPEG and confirm R2 receives it and API marks it complete.
- [ ] Test a file over 25 MB, a non-image, an expired QR, and a full upload limit.
- [ ] Restart the API container and confirm the host session remains valid.

## Network access roadmap

| User | Recommended access | Reason |
|---|---|---|
| Developer/administrator | Tailscale | Private encrypted access to the Windows laptop without router port forwarding. |
| Guests at a real event | Cloudflare Tunnel + HTTPS domain | Stable public QR URL without exposing the office router. |
| Local server test | `http://localhost:<WEB_PORT>` | Fast local validation only. |

Do not use the public IP address directly as a guest URL. It is unstable, requires router/firewall exposure, and does not provide HTTPS by itself.
