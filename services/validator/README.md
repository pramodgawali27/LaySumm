# Validator Service

## Purpose
Applies readability, jargon, and factuality checks to generated summaries.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/validator/evaluations` | Run readability + factual consistency checks. | ValidationRequest | ValidationResponse | Idempotent per summaryId. |
| POST | `/api/validator/readability-preview` | Quick readability calculation for drafts. | ReadabilityPreviewRequest | ReadabilityPreviewResponse | N/A |

## DTO Highlights
- ValidationRequest – summaryId, summary text, citations, numeric enforcement toggle.
- ValidationResponse – readability score, issues, numeric findings.
- ReadabilityPreviewRequest – ad-hoc text for UI hints.

## Configuration
- `Validator:JargonListBlob`
- `Validator:MinimumReadability`
- `Validator:NumericTolerance`

## Idempotency & Resiliency
- SummaryId keyed results to avoid recomputation; stored in registry for reuse.
- External fact-check calls (if enabled) use 3 retries + circuit breaker (4/30s).
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
