# Batch Orchestrator Lane A5

## Purpose
Schedules, shards, and monitors high-volume batch summarization across all lanes.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/batch/jobs` | Create batch job and allocate shards. | BatchJobRequest | BatchJobResponse | Idempotency via Idempotency-Key. |
| GET | `/api/batch/jobs/{jobId}` | Fetch job details. | - | BatchJobResponse | Idempotent |
| GET | `/api/batch/jobs/{jobId}/status` | Retrieve progress counters. | - | BatchJobStatusResponse | Idempotent |
| POST | `/api/batch/jobs/{jobId}/cancel` | Cancel job and release shards. | BatchJobCancelRequest | BatchJobResponse | Idempotent per job |

## DTO Highlights
- BatchJobRequest – jobName, documentIds, lane, schedule, priority, redaction toggle.
- BatchJobResponse – status, timestamps, redaction flag, failure reason.
- BatchJobStatusResponse – counters for completed/failed shards.

## Configuration
- `Queues:BatchDispatch`
- `Queues:BatchDlq`
- `Storage:MetadataConnection`

## Idempotency & Resiliency
- Idempotency-Key maps to jobId; deduplicates repeated submissions from CLI/UI.
- Dispatch HTTP calls use 429 aware retries; queue publishing uses Service Bus/SQS retry policy with DLQ.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
