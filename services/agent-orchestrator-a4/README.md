# Agent Orchestrator Lane A4

## Purpose
Agentic workflow orchestrating summarize → validate → refine → release with human-in-loop stops.

## Endpoints
| Method | Path | Purpose | Request DTO | Response DTO | Idempotency / Retry |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/agent-orchestrator/workflows` | Start orchestration for document. | AgentWorkflowRequest | AgentWorkflowResponse | Idempotent via workflowId. |
| POST | `/api/agent-orchestrator/workflows/{workflowId}/actions` | Submit manual override or advance state. | AgentActionRequest | AgentWorkflowResponse | Idempotent per actionId. |
| GET | `/api/agent-orchestrator/workflows/{workflowId}` | Fetch workflow graph. | - | AgentWorkflowView | Cacheable with ETag |

## DTO Highlights
- AgentWorkflowRequest – documentId, lane preferences, validator + reviewer requirements.
- AgentActionRequest – stepId, action type (approve/override/retry), actor.
- AgentWorkflowView – DAG of steps, state transitions, audit trail references.

## Configuration
- `Workflows:SemanticKernelConfig`
- `Queues:AgentProgress`
- `Routing:HumanReviewQueue`

## Idempotency & Resiliency
- WorkflowId minted by orchestrator; actions require unique actionId to avoid duplicates.
- External step execution uses Polly fallback to human review queue after 3 failures.
- Error responses use the shared `ErrorResponse` contract from `Platform.Api`.
- Health checks exposed at `/healthz` and instrumented with OpenTelemetry + Serilog.

## Security
- Enforces JWT bearer auth (OIDC).
- Applies RBAC via role claims (`admin`, service-specific roles).
- Audit events are emitted to the audit service using the shared correlation ID.
