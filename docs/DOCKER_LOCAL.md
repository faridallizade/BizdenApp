# Bizdən — Docker local stack

## Ports

| Service | Host port | Container port |
|---|---:|---:|
| Web | 55173 | 80 |
| API | 55080 | 8080 |
| PostgreSQL | 55432 | 5432 |

These ports intentionally avoid the currently used 1433, 1435, 5432, 5080, 5173 and 6379 ports.

## Start

```bash
chmod +x scripts/local-up.sh scripts/local-down.sh
./scripts/local-up.sh
```

The `migrate` container waits for PostgreSQL and applies the EF Core `InitialCreate` migration before the API starts.

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
