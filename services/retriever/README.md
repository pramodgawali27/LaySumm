# Retriever Service

## Purpose
Hybrid BM25 + vector retrieval for question/section targeting with citation scoring.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/retriever/search` | Execute hybrid search for given query and filters. | SearchRequest | SearchResponse | Idempotent by searchId. |
| POST | `/api/retriever/index/refresh` | Refresh BM25 + vector indexes for new documents. | IndexRefreshRequest | IndexRefreshResponse | Idempotent per index + timestamp. |
| GET | `/api/retriever/features` | List retrieval features & weights. | - | FeatureFlagsResponse | Cacheable |

## DTO Highlights
- SearchRequest – query text, section anchors, filters, topK and score weights.
- SearchResponse – ranked hits with chunkId, score breakdown, citation URIs.
- IndexRefreshRequest – documentIds, refresh mode (incremental/full).

## Configuration
- `VectorStore:IndexName`
- `Search:HybridWeight`
- `Queues:RetrieverRefresh`

## Idempotency & Resiliency
- searchId optional; same searchId returns cached response for TTL window.
- Outbound to Cognitive Search/OpenSearch uses 429 aware retry + hedging for read operations.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
