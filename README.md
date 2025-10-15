# LaySumm PLS Platform Monorepo

Production-grade Plain Language Summarization (PLS) platform for medical documents. The solution unifies five delivery lanes inside one architecture: **A1 Prompt-only**, **A2 Fine-tuned**, **A3 Retrieval-Augmented Generation**, **A4 Agentic orchestration**, and **A5 Batch processing**. Every service is implemented as a .NET 8 minimal API with OpenAPI, Serilog + OpenTelemetry telemetry, JWT (OIDC) authN, and Polly-based resiliency. Frontend experiences are provided via Next.js (App Router) for both administrators and end users.

## Architecture Overview
- **Zero-trust microservices** communicate over HTTPS via the internal mesh (gateway-bff, orchestrators, validators, etc.).
- **Event-driven spine**: Service Bus/SQS for ingestion, redaction, chunking, embeddings, retrieval warmers, validator, human-review, and batch dispatch. DLQs capture poison messages with audit service notifications.
- **Shared contracts**: `pkg/platform/Platform.Api` provides typed error responses and validation filters.
- **Observability-first**: Serilog structured logging, OpenTelemetry traces/metrics, aggregated by the observability service, and shipped to Azure Monitor/AWS CloudWatch via OTLP exporters.
- **Security defaults**: OIDC (Auth service) issues JWTs, RBAC enforced per service, secrets pulled from Key Vault/Secrets Manager, and audit trail maintained in dedicated service.
- **Frontends**: gateway-bff exposes aggregated APIs consumed by React/Next apps (admin console, end-user portal) with OIDC login.

### Service Inventory & Contracts
| Service | Responsibilities | Key Endpoints | Core DTOs |
| --- | --- | --- | --- |
| gateway-bff | Facade for web clients, routing to appropriate lane, caching status, merging billing data. | `GET /api/gateway/v1/documents/{id}`, `POST /api/gateway/v1/summaries` | `DocumentStatusQuery`, `DocumentStatusView`, `SummaryDispatchRequest`, `SummaryDispatchResponse` |
| ingestion | Handles uploads, OCR, metadata persistence. | `POST /api/ingestion/jobs`, `GET /api/ingestion/jobs/{id}`, `POST /api/ingestion/jobs/{id}/retry` | `IngestionRequest`, `IngestionJobResponse`, `IngestionRetryRequest` |
| pii-redactor | Privacy-by-default masking, reversible via tokenized maps. | `POST /api/pii-redactor/redactions`, `POST /api/pii-redactor/restorations` | `RedactionRequest`, `RedactionResponse`, `RestorationRequest` |
| chunker-parser | Heading-aware chunking (300–500 tokens) and schema extraction. | `POST /api/chunker/chunks`, `GET /api/chunker/schemas/{documentId}` | `ChunkRequest`, `ChunkResponse`, `DocumentSchemaResponse` |
| embeddings | Embedding generation, index maintenance. | `POST /api/embeddings/vectors`, `POST /api/embeddings/reindex` | `EmbeddingBatchRequest`, `EmbeddingBatchResponse`, `ReindexRequest` |
| retriever | Hybrid BM25 + vector retrieval and index refresh. | `POST /api/retriever/search`, `POST /api/retriever/index/refresh` | `SearchRequest`, `SearchResponse`, `IndexRefreshRequest` |
| summarizer-a1 | Prompt templating lane with few-shot registry. | `POST /api/summarizer/a1/summaries`, `POST /api/summarizer/a1/prompts/validate` | `PromptSummaryRequest`, `PromptSummaryResponse`, `PromptValidationRequest` |
| summarizer-a2 | Fine-tuned/LoRA lane with canary toggles. | `POST /api/summarizer/a2/summaries`, `POST /api/summarizer/a2/adapters/canary` | `FineTuneSummaryRequest`, `FineTuneSummaryResponse`, `AdapterCanaryRequest` |
| summarizer-a3 | Retrieval-augmented summarization with citations. | `POST /api/summarizer-a3/summaries`, `GET /api/summarizer-a3/summaries/{id}` | `SummarizationRequest`, `SummarizationResponse`, `SummarizationStatusResponse` |
| agent-orchestrator-a4 | Agentic workflow (summarize → validate → refine → release) with SK/LangChain equivalent integration. | `POST /api/agent-orchestrator/workflows`, `POST /api/agent-orchestrator/workflows/{id}/actions` | `AgentWorkflowRequest`, `AgentWorkflowResponse`, `AgentActionRequest` |
| validator | FRE > 60 readability target, jargon ban list, numeric fact checks. | `POST /api/validator/evaluations`, `POST /api/validator/readability-preview` | `ValidationRequest`, `ValidationResponse`, `ReadabilityPreviewRequest` |
| human-review | Review queues, diff UI, approvals, audit events. | `GET /api/human-review/queues`, `POST /api/human-review/tasks/{id}/decision` | `QueueListResponse`, `ReviewTaskView`, `ReviewDecisionRequest` |
| batch-orchestrator-a5 | Schedule & shard high-volume jobs, manage retries/DLQs. | `POST /api/batch/jobs`, `GET /api/batch/jobs/{id}`, `POST /api/batch/jobs/{id}/cancel` | `BatchJobRequest`, `BatchJobResponse`, `BatchJobStatusResponse`, `BatchJobCancelRequest` |
| observability | Metrics/log ingestion, alert definitions. | `POST /api/observability/logs`, `POST /api/observability/alerts` | `LogIngestRequest`, `LogIngestResponse`, `AlertDefinitionRequest` |
| audit | Immutable audit logs + tamper-evident digests. | `POST /api/audit/events`, `GET /api/audit/events/{eventId}` | `AuditEventRequest`, `AuditEventResponse`, `AuditSearchRequest` |
| registry | Catalog for models, prompts, policies. | `GET /api/registry/models`, `POST /api/registry/prompts` | `ModelListResponse`, `PromptUpsertRequest`, `PolicyView` |
| auth | OIDC integration, RBAC evaluation, token exchange. | `POST /api/auth/token/exchange`, `POST /api/auth/rbac/evaluate` | `TokenExchangeRequest`, `TokenExchangeResponse`, `RbacEvaluateRequest` |
| billing-cost | Usage metering and cost allocation. | `POST /api/billing/usage`, `GET /api/billing/statements/{tenantId}` | `UsageEventRequest`, `BillingStatementResponse`, `AllocationRuleRequest` |

All service-specific details (config, retries, idempotency, security posture) are documented in `services/<service>/README.md` alongside the C# DTO and endpoint definitions.

### Error Model & Resiliency
- Shared `ErrorResponse` + `FieldError` across services from `Platform.Api`.
- Uniform `ValidationFilter<T>` ensures DataAnnotation enforcement before executing handlers.
- `HttpClientFactory` configured with Polly (retry + circuit breaker) per service.
- Health checks exposed at `/healthz`, aggregated by the observability service.

## Storage & Infrastructure Plan
- **Object Storage**: Azure Blob Storage / Amazon S3 for document binaries, chunk artifacts, diff snapshots.
- **Relational Metadata**: Azure SQL Database / Amazon RDS (PostgreSQL) for job metadata, registry, billing, and audit indexing.
- **Queues & Events**: Azure Service Bus topics & DLQs or AWS SQS/SNS with DLQ for ingestion, redaction, chunking, embeddings, validator feedback, agent orchestration steps, and batch shards. Event Grid/SNS fan-out to analytics pipelines.
- **Vector Store**: Azure Cognitive Search (hybrid) by default; optionally swap to Amazon OpenSearch, Pinecone, or Weaviate via provider adapter in retriever/embeddings services.
- **Secrets & Keys**: Azure Key Vault / AWS Secrets Manager + KMS for encryption keys, redaction maps, and signing keys. Private Link/Interface Endpoints enforce network isolation.
- **Monitoring**: Observability service pushes OTLP data to Azure Monitor (Log Analytics, Application Insights) or AWS CloudWatch + X-Ray.

## Cloud Mappings & IaC Stubs
Code comments and IaC scaffolds (see `ops/iac`):
- **Azure** (`ops/iac/azure/main.bicep`): AKS or Container Apps, Azure API Management in front of gateway-bff, Azure OpenAI + Cognitive Search, Service Bus, Event Grid, Key Vault, Private Link for storage/vector endpoints.
- **AWS** (`ops/iac/aws/main.tf`): EKS/ECS Fargate, API Gateway/ALB, Bedrock (Claude)/OpenAI endpoints, OpenSearch, S3, SQS/SNS, Secrets Manager + KMS, Route 53 records.
- Templates include placeholders for managed identity/IRSA wiring, network security groups/security groups, autoscaling policies, and cost allocation tags.

## Project Structure
```
/services/<service>/src/<Service>.Api    # Minimal API + contracts + resiliency defaults
/services/<service>/tests                # xUnit + FluentAssertions integration tests
/frontends/admin-app                     # Admin Next.js app (OIDC protected)
/frontends/end-user-app                  # End-user Next.js app (upload/status flows)
/pkg/platform/Platform.Api               # Shared validation + error primitives
/ops/iac/azure | aws                     # Bicep/Terraform stubs for cloud resources
/ops/pipelines                           # CI/CD workflow definitions
/tests                                   # End-to-end, load, evaluation placeholders
```

## Approach-Specific Logic
- **Lane A1 (Prompt-only)**: Uses registry-curated prompt templates with few-shot examples, safe completion limits, and quick rollback via feature flags. Canary tokens allow manual override to fall back to base prompts.
- **Lane A2 (Fine-tuned)**: Adapter-aware client supports LoRA toggles, canary rollout, and automatic fallback to base model when evaluation thresholds fail. Canary gating integrated with registry policies and billing.
- **Lane A3 (RAG)**: Per-section retrieval avoids recall gaps, citations stitched with chunk metadata, validator enforces readability + numeric grounding. Summaries store retrieval metadata for audit.
- **Lane A4 (Agentic)**: Semantic Kernel-based orchestrator composes summarize → validate → refine → release sequences. Failed validation triggers auto-rewrite; human-in-loop stop when escalations or guardrails trip.
- **Lane A5 (Batch)**: Batch orchestrator shards work by tenant/lane, tracks costs, applies backpressure via queue depth, and supports PTU/dedicated throughput by tagging shards with cost centers.

## Quality & Compliance
- Readability: Validator enforces Flesch Reading Ease ≥ 60 and Flesch-Kincaid grade compliance. Jargon ban list sourced from medical plain-language guidelines.
- Numeric fidelity: Validator cross-checks numeric values against retrieved evidence; failures escalate to human review.
- Privacy-by-default: PII redactor masks PHI/PII with reversible tokens stored in secure key vault; human reviewers see masked data unless authorized.
- Regional routing: Registry policies pin workloads to compliant regions (Azure region pairs / AWS regions) and ensure data residency.
- Audit & retention: Audit service maintains immutable log with configurable retention; all services emit correlation IDs for traceability.

## CI/CD & Environments
- **CI/CD**: GitHub Actions workflows (see `ops/pipelines/github-actions.yml`) run dotnet build/test, npm lint, SAST (CodeQL), container scans, and SBOM generation before deploying via IaC.
- **Environments**:
  - `dev`: PoC focus (Lane A1 prompt-only) with feature flags to stub other lanes.
  - `stage`: Enables A1 + A3 with nightly evaluation against golden datasets before promotion.
  - `prod`: All lanes (A1–A5) with canary deployment for summarizer lanes and evaluation gates.
- **Deployment strategy**: Blue/green for stateless services via AKS/EKS; summarizer lanes use traffic-split canaries. Batch orchestrator relies on golden-set evaluation before enabling new adapters or prompts.

## Testing Strategy
- Unit and minimal integration tests per service (xUnit + WebApplicationFactory) validating HTTP contracts.
- Planned `/tests` folders for:
  - **e2e**: orchestrated scenario tests (document → summary → review).
  - **load**: k6/Gatling stress tests for ingestion and summarizer lanes.
  - **eval**: automatic quality benchmarks on curated medical corpora.

## Frontend Highlights
- `frontends/admin-app/app/(authenticated)/reviews/page.tsx` renders review queues with React Query stub hooking into gateway-bff.
- `frontends/end-user-app/app/upload/page.tsx` and `app/status/[id]/page.tsx` demonstrate end-user upload + tracking flows with polling placeholders.

## Next Steps
- Wire HTTP clients to actual service URLs via strongly typed SDKs.
- Implement persistent stores (EF Core / Dapper) referencing the relational model.
- Expand CI/CD pipeline definitions and IaC modules with parameterized environments.
