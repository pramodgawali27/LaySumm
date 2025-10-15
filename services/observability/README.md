# Observability Service

## Purpose
Centralizes metrics, traces, and log streaming with tenant-aware routing.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/observability/logs` | Ingest structured logs from services. | LogIngestRequest | LogIngestResponse | Idempotent per logId. |
| GET | `/api/observability/dashboards/{dashboardId}` | Return dashboard metadata + query templates. | - | DashboardView | Cacheable |
| POST | `/api/observability/alerts` | Create/update alert definitions. | AlertDefinitionRequest | AlertDefinitionResponse | Idempotent per alertId |

## DTO Highlights
- LogIngestRequest – logId, tenant, payload, severity.
- DashboardView – metrics queries and layout definitions.
- AlertDefinitionRequest – ruleId, thresholds, notification channels.

## Configuration
- `Telemetry:OtlpEndpoint`
- `Storage:MetricsDatabase`
- `Alerts:WebhookUrl`

## Idempotency & Resiliency
- LogId required from source service to dedupe; dashboards cached per tenant.
- Writes to metrics store use retry/backoff; alert webhooks use fallback queue after 3 failures.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
