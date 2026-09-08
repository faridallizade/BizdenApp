# Monitoring

The API exposes Prometheus-compatible counters at `GET /metrics`. The endpoint is disabled until `Monitoring__MetricsApiKey` is set. Scrapers must send that value in the `X-Metrics-Key` request header.

Use a long random value only in the production secret store; do not commit it to `.env`.

Metrics available:

- `bizden_http_requests_error_total`: API responses with a 5xx status.
- `bizden_uploads_completed_total` and `bizden_uploads_failed_total`: verified upload success and failures.
- `bizden_r2_failures_total`: failed R2 reads, verification, deletes, or derivative uploads.
- `bizden_reservation_expiries_total`: uploads reserved but not completed within 15 minutes.

Recommended alerts:

- API error rate is over 5% for five minutes.
- `bizden_r2_failures_total` increases during a five-minute period.
- Upload failure rate is over 10% for ten minutes.
- Reservation expiries rise unexpectedly (for example, more than 20 in 15 minutes).
- `/health/ready` returns anything except HTTP 200.

Counters are process-local and reset whenever the API container restarts. For durable history and alerts, scrape them with Prometheus, Grafana Cloud, or another Prometheus-compatible monitoring service.
