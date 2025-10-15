# Human Review Service

## Purpose
Manages review queues, diff views, and approvals for regulated workflows.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/human-review/queues` | List queues and counts by priority. | QueueQuery | QueueListResponse | Cacheable |
| GET | `/api/human-review/tasks/{taskId}` | Fetch task details with diff payload. | - | ReviewTaskView | Idempotent |
| POST | `/api/human-review/tasks/{taskId}/decision` | Approve or request changes. | ReviewDecisionRequest | ReviewTaskView | Idempotent per decisionId |

## DTO Highlights
- QueueListResponse – queueId, SLA minutes, open tasks.
- ReviewTaskView – summary diff, validator results, audit pointers.
- ReviewDecisionRequest – decisionId, outcome, comments, escalation target.

## Configuration
- `Queues:HumanReviewTopic`
- `Storage:DiffBlobContainer`
- `FeatureFlags:BypassForRoles`

## Idempotency & Resiliency
- Decisions require client-supplied decisionId; duplicates ignored.
- Notifier (email/Teams) uses retry/backoff; queue ack uses at-least-once semantics with DLQ.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
