# PostgreSQL local setup

Bizdən uses PostgreSQL for authentication, events, QR invitations, public galleries, media jobs and audit records. Migrations are committed; applying them is required before running the API against a fresh database.

## Your one-time task

Create a local PostgreSQL database named `bizden_dev`. Docker, a local PostgreSQL installation, Neon or Supabase development database are all acceptable.

Then store its connection string as a .NET user-secret. This keeps the password out of Git:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=bizden_dev;Username=postgres;Password=YOUR_PASSWORD" --project apps/api/Bizden.Api
```

## Apply the schema locally

After the connection string is configured, run:

```bash
./.tools/dotnet-ef database update \
  --project src/Bizden.Infrastructure/Bizden.Infrastructure.csproj \
  --startup-project apps/api/Bizden.Api/Bizden.Api.csproj \
  --context BizdenDbContext
```

This applies every committed migration, including authentication, photo derivatives, exports, public galleries and admin management. Run it again after pulling a new migration.

## Production check

Before deploying a new API build, back up the database and run the same command with the production connection string injected through the secret store. Confirm `__EFMigrationsHistory` contains the newest migration, then check `/health/ready`.
