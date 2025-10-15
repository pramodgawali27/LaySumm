# Billing & Cost Service

## Purpose
Tracks usage events, cost attribution, and exposes billing summaries for tenants.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/billing/usage` | Ingest usage event for lane/model invocation. | UsageEventRequest | UsageEventResponse | Idempotent per eventId |
| GET | `/api/billing/statements/{tenantId}` | Retrieve billing statement for period. | - | BillingStatementResponse | Cacheable |
| POST | `/api/billing/allocations` | Update cost allocation rules. | AllocationRuleRequest | AllocationRuleResponse | Idempotent per ruleId |

## DTO Highlights
- UsageEventRequest – eventId, tenantId, modelId, tokens, unitCost.
- BillingStatementResponse – charges grouped by lane/model/time.
- AllocationRuleRequest – ruleId, cost center mapping, effective dates.

## Configuration
- `Storage:BillingDatabase`
- `Queues:UsageIngest`
- `Cost:DefaultRateCard`

## Idempotency & Resiliency
- eventId unique; duplicates ignored; statements cached by tenant/month.
- Writes to billing DB use retry/backoff; export to data warehouse uses circuit breaker (3/30s).
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
