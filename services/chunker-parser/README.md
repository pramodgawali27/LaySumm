# Chunker Parser Service

## Purpose
Splits documents into heading-aware chunks (300–500 tokens) and extracts structure metadata.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/chunker/chunks` | Chunk uploaded document. | ChunkRequest | ChunkResponse | Idempotency via documentId+revision. |
| POST | `/api/chunker/rechunk` | Rechunk for different audience or token target. | RechunkRequest | ChunkResponse | Idempotency based on jobId. |
| GET | `/api/chunker/schemas/{documentId}` | Return parser schema + detected headings. | - | DocumentSchemaResponse | Cacheable. |

## DTO Highlights
- ChunkRequest – documentId, storageUri, defaultTokenTarget, heuristics flags.
- ChunkResponse – chunkIds, heading metadata, cross references.
- DocumentSchemaResponse – outline + section to chunk map.

## Configuration
- `Parsing:MaxTokens`
- `Parsing:HeadingModel`
- `Storage:BlobContainer`

## Idempotency & Resiliency
- Document revision ID is required; service reuses cached chunk maps when same revision requested.
- Polly-based retries around OCR text download + embedding fan-out.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
