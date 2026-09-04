# PostgreSQL — Phase 2 local setup

Phase 2 has created the schema migration, but no database has been created or modified because a local PostgreSQL connection was not provided.

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

This applies the existing `InitialCreate` migration. It creates the metadata tables only; no R2 storage, authentication flow, QR endpoint or upload feature has been added yet.
