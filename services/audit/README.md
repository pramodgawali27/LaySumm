# Audit Service

## Purpose
Captures immutable audit events for compliance and produces tamper-evident digests.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/audit/events` | Persist audit trail event. | AuditEventRequest | AuditEventResponse | Idempotent per eventId |
| GET | `/api/audit/events/{eventId}` | Fetch single audit event. | - | AuditEventView | Idempotent |
| POST | `/api/audit/search` | Search audit events with filters. | AuditSearchRequest | AuditSearchResponse | Paginated |

## DTO Highlights
- AuditEventRequest – eventId, actor, action, resource, metadata.
- AuditEventView – event payload + hash chain info.
- AuditSearchRequest – time range, actor filters, resource filters.

## Configuration
- `Storage:AuditDatabase`
- `Hashing:SigningKeyId`
- `Retention:Days`

## Idempotency & Resiliency
- eventId unique ensures single insert; duplicates return existing record.
- Database writes use transient retry; signing operations fallback to KMS secondary region.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
