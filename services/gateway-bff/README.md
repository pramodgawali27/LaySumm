# Gateway BFF Service

## Purpose
Back-end-for-frontends that aggregates orchestrator, status, and billing calls for admin and end-user UIs.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/gateway/v1/status` | Lightweight readiness probe. | – | ServiceStatus | N/A |
| POST | `/api/gateway/v1/summaries` | Execute the ingestion → RAG summarization → validation pipeline (lane A3). | SummaryDispatchRequest | SummaryDispatchResponse | `Idempotency-Key` header dedupes submissions. |
| GET | `/api/gateway/v1/documents/{documentId}` | Aggregate ingestion status, summary metadata, and validation results. | – | DocumentStatusView | Cached in-memory and refreshed via upstream APIs. |
| POST | `/api/gateway/v1/batch` | Proxy batch-orchestrator lane A5 scheduling. | BatchGatewayRequest | BatchGatewayResponse | `Idempotency-Key` header dedupes batch creation. |

## DTO Highlights
- SummaryDispatchRequest – includes retrieval hints, per-section plans, and readability targets.
- SummaryDispatchResponse – exposes generated sections, validation results, and human review flag.
- DocumentStatusView – merges stored state with live ingestion + summarizer status.

## Configuration
- `Services:IngestionBaseUrl`
- `Services:SummarizerA3BaseUrl`
- `Services:ValidatorBaseUrl`
- `Services:BatchBaseUrl`

## Idempotency & Resiliency
- `InMemoryDocumentStateStore` stores responses keyed by document + idempotency.
- Outbound HTTP clients use Polly jittered backoff (3x) and circuit breakers (4x 500/429).
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
