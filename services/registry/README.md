# Registry Service

## Purpose
Authoritative catalog for models, prompts, adapters, and policy definitions.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/registry/models` | List registered models and capabilities. | - | ModelListResponse | Cacheable |
| POST | `/api/registry/prompts` | Create/Update prompt template definitions. | PromptUpsertRequest | PromptDefinition | Idempotent per promptId version |
| GET | `/api/registry/policies/{policyId}` | Fetch policy used for routing decisions. | - | PolicyView | Cacheable |

## DTO Highlights
- ModelListResponse – provider, region, capabilities, cost metadata.
- PromptUpsertRequest – promptId, version, template, guardrail tags.
- PolicyView – summarization lane weights, feature flags.

## Configuration
- `Storage:RegistryDatabase`
- `Cache:RedisConnection`
- `FeatureFlags:DefaultPolicyId`

## Idempotency & Resiliency
- Prompt version acts as natural key; writes check concurrency tokens.
- Database writes use retry/backoff; cache invalidation retried with fallback to eventual consistency.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
