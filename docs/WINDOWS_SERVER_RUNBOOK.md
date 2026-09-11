# Bizdən — Windows server runbook

This guide runs the complete stack on one Windows laptop with Docker Desktop and WSL2. The public entry point is the web container; PostgreSQL and the API are not exposed as host ports.

## One-time prerequisites

- Windows 10/11 with WSL2 enabled.
- Docker Desktop set to the Linux containers / WSL2 backend.
- Git access to the `BizdenApp` repository.
- Root `.env` file created from `.env.example`, containing the R2 values and `WEB_PORT`.

## Configuration

Create `.env` at the repository root:

```env
WEB_PORT=58081
R2__Endpoint=https://ACCOUNT_ID.r2.cloudflarestorage.com
R2__Bucket=bizden-media
R2__AccessKeyId=YOUR_ACCESS_KEY_ID
R2__SecretAccessKey=YOUR_SECRET_ACCESS_KEY
```

Only `WEB_PORT` is reachable from Windows. Do not add API or PostgreSQL port mappings unless there is a specific troubleshooting need.

## Start or update

```powershell
cd C:\Users\RbcUser\Desktop\project\BizdenApp
git pull
docker compose down
docker compose up --build -d
docker compose ps
```

Expected running services are `bizden-web`, `bizden-api`, and `bizden-postgres`. `bizden-migrate-1` exits successfully after applying database migrations; that is expected.

`bizden-migrate-1` must apply the full current migration set (authentication, QR, media processing, galleries and admin). If it fails, do not start an event: inspect its logs with `docker compose logs migrate`, fix the database connection, and rerun the stack.

Open:

```text
http://localhost:58081
http://localhost:58081/health
```

## Port unavailable

Windows can reserve a port for WSL, Hyper-V, VPN software, or another application. This appears as `ports are not available` or `access permissions` in Docker output.

1. In `.env`, change `WEB_PORT` to another value, for example `58082`.
2. Run `docker compose down` and `docker compose up --build -d` again.
3. Open the URL with the new port.

To see whether a process is actively using a port:

```powershell
Get-NetTCPConnection -LocalPort 58081 -ErrorAction SilentlyContinue
```

If it returns nothing but Docker still refuses the port, Windows has most likely reserved it. Pick another port instead of terminating system processes.

## Docker Desktop / WSL unavailable

If Docker reports that WSL is unresponsive:

1. Restart Windows. This is the reliable recovery when `wsl -l -v` hangs.
2. Verify `wsl -l -v` returns promptly.
3. Open Docker Desktop and wait for its engine to start.
4. Run `docker info`; the `Server` section must not include an error.

## R2 uploads

The browser uploads photos directly to private Cloudflare R2 using a ten-minute signed URL. Configure R2 CORS for each browser origin used for testing or deployment. For local use this includes:

```json
[
  {
    "AllowedOrigins": ["http://localhost:58081"],
    "AllowedMethods": ["PUT", "HEAD"],
    "AllowedHeaders": ["Content-Type"],
    "ExposeHeaders": ["ETag"],
    "MaxAgeSeconds": 3600
  }
]
```

When a Cloudflare Tunnel and domain are added, add that HTTPS domain as another origin. Do not use a wildcard origin.

## Before public usage

- Use Cloudflare Tunnel with a real domain and HTTPS; do not expose the laptop by router port forwarding.
- Install Tailscale only for private administrator access; guests should use the HTTPS domain.
- Replace the current learning/development R2 credentials with a new bucket-scoped token.
- Use a strong `POSTGRES_PASSWORD` in `.env`.
- Back up the Docker PostgreSQL volume and verify restore steps.
- Confirm a test OTP, QR upload, gallery PIN, ZIP export and admin login after each production update.
- Keep Windows awake, connected to power, and prevent automatic restarts during events.
