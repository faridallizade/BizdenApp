# Bizdən — production launch

This repository has a production Compose override. It changes ASP.NET to `Production`, keeps the web container bound only to localhost, and lets Caddy be the only public listener on ports 80 and 443.

## Required external setup

1. Point `PUBLIC_HOST` at the server public IP. For the current DuckDNS hostname, confirm with `dig +short bizden.duckdns.org`.
2. Forward TCP ports 80 and 443 from the router/firewall to this server. Do not forward the API, PostgreSQL, or `WEB_PORT`.
3. Put the final R2 browser origin in the bucket CORS rule: `https://PUBLIC_HOST`. Keep `PUT` and `HEAD`, allow `Content-Type`, and do not use `*`.
4. Rotate development R2 credentials and set a strong `POSTGRES_PASSWORD` in the server `.env` file.
5. Configure an encrypted PostgreSQL backup outside this machine and complete one restore test.

## Start production

Create a server-only `.env` file from `.env.example`, set `PUBLIC_HOST`, database credentials, and R2 credentials, then run:

```bash
docker compose -f compose.yaml -f compose.production.yaml up --build -d
docker compose ps
```

Caddy requests the TLS certificate automatically after public TCP 80/443 reach this server. Verify:

```bash
curl -I https://$PUBLIC_HOST
curl https://$PUBLIC_HOST/health
curl https://$PUBLIC_HOST/health/ready
```

The browser must show HTTPS before host login is tested. In production the auth and CSRF cookies are marked `Secure`, so local HTTP login is intentionally not valid.

## First acceptance test

1. Register or log in as a host over `https://PUBLIC_HOST`.
2. Create an active event and QR code.
3. Scan it from iPhone Safari and Android Chrome.
4. Upload a JPEG, then verify it in the host gallery and download it.
5. Confirm expired, inactive, over-limit, oversized, and non-image uploads fail.

## Certificate troubleshooting

If Caddy cannot obtain a certificate, stop and fix public routing before retrying. The ACME challenge must reach this exact Caddy container; an HTML response from another web server means router forwarding or port ownership is wrong.
