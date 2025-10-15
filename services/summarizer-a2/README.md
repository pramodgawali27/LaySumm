# Summarizer Lane A2

## Purpose
Fine-tuned/adapter-based lane supporting LoRA toggles and canary promotion.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/summarizer/a2/summaries` | Invoke fine-tuned model with adapter selection. | FineTuneSummaryRequest | FineTuneSummaryResponse | Idempotency via jobId. |
| POST | `/api/summarizer/a2/adapters/canary` | Flip canary traffic split between base and adapter. | AdapterCanaryRequest | AdapterCanaryResponse | Idempotent per adapterId. |
| GET | `/api/summarizer/a2/models` | List available base models + adapter metadata. | - | AdapterModelList | Cacheable |

## DTO Highlights
- FineTuneSummaryRequest – documentId, adapterId, fallback lane, guardrail config.
- AdapterCanaryRequest – adapterId, trafficPercent, expiresAt.
- AdapterModelList – baseModelId, adapterId, training summary.

## Configuration
- `Models:FineTuneEndpoint`
- `Models:DefaultAdapterId`
- `FeatureFlags:CanaryPercentage`

## Idempotency & Resiliency
- Job id + adapterId ensure dedupe; LoRA inference caches base prompts per adapter.
- LLM invocation uses 3 retries with jitter and circuit breaker (4/60s).
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
