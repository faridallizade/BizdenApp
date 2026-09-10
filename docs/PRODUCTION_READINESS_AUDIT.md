# Bizdən — Production readiness

Last reviewed: 2026-09-10

## Current architecture

- Linux VPS, Docker Compose, Caddy HTTPS reverse proxy.
- React/Vite web container, .NET 8 API, PostgreSQL and Cloudflare R2.
- API and PostgreSQL remain private to the Docker network.
- Host cookies are secure in production; mutations require same-origin CSRF tokens.
- R2 stores originals, previews, thumbnails and background ZIP exports.

## Implemented

- Host registration email OTP, `@farid.test` development registration bypass, login and password reset.
- Image validation/processing worker, preview/thumbnail generation and quarantine state.
- Per-event invitation QR upload controls, upload reservations and rate limiting.
- Multi-tenant event ownership authorization.
- PIN-protected, selected-photo public galleries.
- Background ZIP jobs, status polling, 24-hour signed download links and ready email.
- Metrics endpoint protected by `Monitoring__MetricsApiKey`.
- Admin host/event/audit APIs with event soft-delete.

## Required before public launch

1. Run the full browser acceptance suite on the VPS after database migrations.
2. Configure R2 CORS for only the final HTTPS origin, allowing `PUT` and `HEAD` with `Content-Type`.
3. Confirm SMTP delivery, password reset, gallery-ready and export-ready emails from production.
4. Configure external monitoring to scrape `/metrics` using the metrics key and add alert rules.
5. Implement and test encrypted PostgreSQL backup/restore; R2 data and database metadata must both be recoverable.
6. Rotate all development R2 and SMTP credentials; keep `.env` server-only.
7. Complete load, abuse and accessibility testing before guest-facing launch.

## Deferred product scope

- Payments, product plans, promocodes and cashback/coins.
- Backup/recovery implementation and monitoring alert configuration.
- Expanded automated test coverage.
