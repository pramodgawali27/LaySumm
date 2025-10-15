# PII Redactor Service

## Purpose
Performs privacy-by-default masking of PHI/PII before summarization and stores reversible maps in Key Vault/KMS.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/pii-redactor/redactions` | Apply masking to plaintext chunks. | RedactionRequest | RedactionResponse | Idempotency-Key + content hash ensures dedupe. |
| POST | `/api/pii-redactor/restorations` | Restore masked tokens for authorized viewers. | RestorationRequest | RestorationResponse | Single-use restoration tokens. |

## DTO Highlights
- RedactionRequest – documentId, tenantId, content array, masking profileId.
- RedactionResponse – redacted content, placeholder map id, checksum.
- RestorationRequest – mapId, segmentIds, viewer role.

## Configuration
- `KeyManagement:VaultUri`
- `KeyManagement:KeyId`
- `Redaction:BypassRoles`

## Idempotency & Resiliency
- Content hash + tenant ensures no duplicate masking charges; restoration tokens expire after configurable TTL.
- Calls to Key Vault/Secrets Manager use 5x retry with decorrelated jitter.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
