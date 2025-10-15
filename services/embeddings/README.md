# Embeddings Service

## Purpose
Produces and maintains semantic embeddings for chunked content, triggers vector store updates.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/embeddings/vectors` | Generate embeddings for chunk batch. | EmbeddingBatchRequest | EmbeddingBatchResponse | Idempotency per chunkSha. |
| POST | `/api/embeddings/reindex` | Backfill or rebuild index for document set. | ReindexRequest | ReindexResponse | Job idempotency by document set hash. |
| GET | `/api/embeddings/models` | List supported embedding models + configs. | - | EmbeddingModelList | N/A |

## DTO Highlights
- EmbeddingBatchRequest – chunk descriptors with text or storage pointer.
- EmbeddingBatchResponse – vector ids, dimensionality, metadata echoes.
- ReindexRequest – filter criteria, priority, dryRun toggle.

## Configuration
- `VectorStore:Endpoint`
- `VectorStore:IndexName`
- `Models:EmbeddingDefault`

## Idempotency & Resiliency
- Chunk hash stored with vectorId; repeated requests reuse existing vector and skip inference.
- Vector store operations use retry (429 aware) and circuit breaker (3 failures/15s).
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
