# Bizdən — Local Development

## Required tools

- .NET 8 SDK
- Node.js 20+ and npm

## API

```bash
dotnet run --project apps/api/Bizden.Api
```

The API has a health endpoint at `/health`.

## Frontend

```bash
npm run dev --prefix apps/web
```

## Validation

```bash
dotnet build Bizden.slnx
dotnet test Bizden.slnx
npm run lint --prefix apps/web
npm run build --prefix apps/web
```

## Configuration

`.env.example` contains only browser-safe configuration. Database, R2 and authentication credentials must be configured later through local user-secrets or the deployment platform secret store.
