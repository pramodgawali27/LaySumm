# Summarizer Lane A3

## Purpose
Retrieval-augmented summarization with per-section retrieval, citation stitching, and readability enforcement.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/summarizer-a3/summaries` | Generate RAG summary using retriever + validator heuristics. | SummarizationRequest | SummarizationResponse | Idempotency via upstream jobId and summaryId cache. |
| GET | `/api/summarizer-a3/summaries/{summaryId}` | Lookup summary status and metadata. | - | SummarizationStatusResponse | Cacheable. |

## DTO Highlights
- SummarizationRequest – sections, retrieval options, audience, readability floor.
- SummarizationResponse – sections with readability + citations.
- SummarizationStatusResponse – progress for orchestration/human review.

## Configuration
- `Rag:RetrieverBaseUrl`
- `Rag:SummarizationModel`
- `Rag:MaxSectionTokens`

## Idempotency & Resiliency
- SummaryId derived from jobId+sections to avoid duplicate billing; cached in registry.
- Retriever + model calls use 3 retries with 429 awareness and circuit breaker 4/30s.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
