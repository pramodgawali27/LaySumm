# Ingestion Service

## Purpose
Handles document upload, OCR normalization, and metadata extraction prior to downstream processing.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/ingestion/jobs` | Create ingestion job, allocate storage location, enqueue OCR if required. | IngestionRequest | IngestionJobResponse | Idempotency-Key header dedupes uploads. |
| GET | `/api/ingestion/jobs/{jobId}` | Retrieve current state, timestamps, and failure details. | - | IngestionJobResponse | Idempotent read. |
| POST | `/api/ingestion/jobs/{jobId}/retry` | Reset job and requeue processing. | IngestionRetryRequest | IngestionJobResponse | Idempotent per job+reason. |

## DTO Highlights
- IngestionRequest – documentId, sourceUri, OCR toggle, locale, metadata, priority.
- IngestionJobResponse – jobId, status, timestamps, metadata copy, lastError.
- IngestionRetryRequest – operator reason captured for audit.

## Configuration
- `Storage:BlobContainer`
- `Queues:IngestionTopic`
- `Ocr:EndpointUrl`

## Idempotency & Resiliency
- Uses Idempotency-Key header with persistent store mapping to jobId; retries fan out via Service Bus/SQS with DLQ.
- Document storage + OCR HTTP clients use exponential backoff (2^n) and circuit breaker 5/30s via Polly.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
