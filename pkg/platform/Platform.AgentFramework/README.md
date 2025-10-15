# Platform.AgentFramework

Shared agent blueprints and workflow definitions for the LaySumm platform built on top of the Microsoft Agent Framework for .NET. The package provides strongly typed descriptors for each agent role together with helper extensions that compose the Plain Language Summary (PLS) workflow that coordinates ingestion, retrieval, summarisation, visual generation, and assembly.

## Contents
- `Agents/` — strongly typed metadata and DI registration helpers for Reader, Indexer, Retriever, Summarizer, Visualizer, Assembler, and Supervisor agents.
- `Tools/` — descriptors for the domain-specific tools that are exposed to the agents.
- `Workflows/` — workflow blueprints that wire all agents into the orchestrated pipeline with checkpoints and human-in-the-loop stops.

## Usage
Reference this project from services that host agentic workflows (for example `services/agent-orchestrator-a4`). Register the agents during startup via:

```csharp
builder.Services.AddLaySummAgents();
var workflow = builder.Services.BuildServiceProvider()
    .GetRequiredService<PlainLanguageSummaryWorkflow>();
```

Each agent blueprint encapsulates the Microsoft Agent Framework `ChatClientAgentOptions` instructions so production hosts can instantiate concrete `ChatClientAgent` instances using their configured `IChatClient` providers (Azure OpenAI by default, OpenAI as a fallback). The workflow blueprint exposes the directed graph that the supervisor executes and can be materialised into a `Workflow` object via the Agent Framework APIs.
