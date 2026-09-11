# Bizdən — Docker local stack

## Ports

| Service | Host port | Container port |
|---|---:|---:|
| Web | `WEB_PORT` (default: 58081) | 80 |
| API | Not exposed | 8080 |
| PostgreSQL | Not exposed | 5432 |

The API and database are only available inside Docker. Nginx in the web container proxies `/api` to the API container. This avoids host-port conflicts and allows the same web URL to work from another device.

On Windows, set `WEB_PORT` in the root `.env` file. If the port is occupied or reserved by Windows/WSL, choose another unused value such as `58082`, then start the stack again. Open `http://localhost:<WEB_PORT>`.

## Start

```bash
chmod +x scripts/local-up.sh scripts/local-down.sh
./scripts/local-up.sh
```

The `migrate` container waits for PostgreSQL and applies every committed EF Core migration before the API starts. This includes authentication, QR invitations, photo derivatives, exports, public galleries and admin management.

For a VPS deployment, use the production environment file and a managed or backed-up PostgreSQL instance; this local compose stack is for development and Windows testing only.

## Stop

```bash
./scripts/local-down.sh
```

To also remove local database data, run this destructive command only when data can be discarded:

```bash
docker compose down --volumes
```

## Local-only credentials

The default local database credentials are intentionally limited to Docker development. Change them before sharing the environment or use a local `.env` file with `POSTGRES_DB`, `POSTGRES_USER` and `POSTGRES_PASSWORD`. Never use these defaults in production.
