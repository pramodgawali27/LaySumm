# Auth Service

## Purpose
OIDC integration façade providing service-to-service token exchange and RBAC introspection.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/auth/token/exchange` | Exchange backend credentials for scoped access tokens. | TokenExchangeRequest | TokenExchangeResponse | Idempotent per nonce |
| GET | `/api/auth/oidc/.well-known` | Expose downstream configuration metadata. | - | OidcMetadata | Cacheable |
| POST | `/api/auth/rbac/evaluate` | Evaluate RBAC rules for principal. | RbacEvaluateRequest | RbacEvaluateResponse | Idempotent |

## DTO Highlights
- TokenExchangeRequest – subject, scopes, audience, proof of possession.
- TokenExchangeResponse – accessToken, expiresIn, refreshToken (optional).
- RbacEvaluateResponse – allowed flag, reasons, obligations.

## Configuration
- `Oidc:Authority`
- `Oidc:ClientId`
- `Cache:TokenRedis`

## Idempotency & Resiliency
- Nonce ensures exchanges cannot be replayed; RBAC evaluations cached for TTL per principal.
- Upstream IdP calls use retry/backoff with circuit breaker (5/60s).
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
