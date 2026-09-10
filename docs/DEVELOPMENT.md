# Bizdən — Local development

## Tələblər

- .NET 8 SDK
- Node.js 20+ və npm
- Docker Desktop (PostgreSQL/R2-lə birlikdə end-to-end yoxlama üçün)

## API

```bash
dotnet run --project apps/api/Bizden.Api/Bizden.Api.csproj
```

API health endpoint-ləri: `/health` və database readiness üçün `/health/ready`.

## Frontend

```bash
npm install --prefix apps/web
npm run dev --prefix apps/web
```

Vite tətbiqi `/api` sorğularını eyni origin-dən edir. Protected host mutation sorğuları `src/lib/api.ts` vasitəsilə CSRF tokenini avtomatik alır və göndərir.

## Konfiqurasiya

Local və production secret-ləri Git-ə düşməyən root `.env` faylındadır. Minimum server konfiqurasiyası:

- `POSTGRES_*`
- `R2__Endpoint`, `R2__Bucket`, `R2__AccessKeyId`, `R2__SecretAccessKey`
- `Smtp__Host`, `Smtp__UserName`, `Smtp__Password`, `Smtp__From`
- `Monitoring__MetricsApiKey`

OTP-lər SMTP ilə göndərilir. `@farid.test` yalnız qeydiyyat üçün email verification bypass-dır; real domain-lərdə OTP məcburidir.

## Build yoxlaması

```bash
dotnet build apps/api/Bizden.Api/Bizden.Api.csproj --no-restore
npm run build --prefix apps/web
npm run lint --prefix apps/web
```

Test coverage və tam E2E suite ayrıca son quality mərhələsində genişləndiriləcək.
