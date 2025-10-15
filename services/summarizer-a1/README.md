# Summarizer Lane A1

## Purpose
Prompt-template lane using prompt registry and safe completion guardrails.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/summarizer/a1/summaries` | Create summary using prompt-only approach. | PromptSummaryRequest | PromptSummaryResponse | Idempotency via orchestration job id. |
| POST | `/api/summarizer/a1/prompts/validate` | Validate prompt template outputs against policies. | PromptValidationRequest | PromptValidationResponse | Deterministic per template + version. |
| GET | `/api/summarizer/a1/prompts/{promptId}` | Return resolved prompt with few-shot examples. | - | PromptDefinition | Cacheable |

## DTO Highlights
- PromptSummaryRequest – documentId, promptId, audience, safety caps.
- PromptValidationRequest – template body + sample variables.
- PromptDefinition – prompt text, few-shot examples, metadata.

## Configuration
- `Models:CompletionModel`
- `Prompts:RegistryUrl`
- `Safety:MaxOutputTokens`

## Idempotency & Resiliency
- Orchestrator supplies jobId; service reuses cached completions when same doc+prompt combination seen.
- LLM calls use retry with safe max (2) and fallback to alternate provider configured via registry.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
