using System.Collections.Generic;
using System.Linq;
using Platform.AgentFramework.Agents;

namespace Platform.AgentFramework.Workflows;

public sealed class PlainLanguageSummaryWorkflow
{
    private readonly ILaySummAgentBlueprints _blueprints;

    public PlainLanguageSummaryWorkflow(ILaySummAgentBlueprints blueprints)
    {
        _blueprints = blueprints;
    }

    public WorkflowDescriptor Describe() => new(
        Name: "pls-supervisor-workflow",
        Description: "Supervisor workflow that orchestrates Reader → Indexer → Retriever/Summarizer → Visualizer → Assembler with human review checkpoints.",
        Agents: _blueprints.All.Select(b => b.Name).ToArray(),
        Edges: BuildEdges(),
        Checkpoints: BuildCheckpoints(),
        Metadata: new Dictionary<string, string>
        {
            ["checkpointing"] = "Enabled after ingestion and after assembly",
            ["retry-policy"] = "Exponential backoff with 3 attempts per agent",
            ["default-model-provider"] = "azure-openai",
            ["fallback-model-provider"] = "openai"
        });

    private static IReadOnlyCollection<WorkflowEdge> BuildEdges() => new List<WorkflowEdge>
    {
        new(AgentNames.Supervisor, AgentNames.Reader, "start"),
        new(AgentNames.Reader, AgentNames.Indexer, "ingest_complete"),
        new(AgentNames.Indexer, AgentNames.Retriever, "has_prompt == true"),
        new(AgentNames.Indexer, AgentNames.Summarizer, "has_prompt == false // iterate sections in-order"),
        new(AgentNames.Retriever, AgentNames.Summarizer, "retrieval_complete"),
        new(AgentNames.Summarizer, AgentNames.Visualizer, "summary_ready"),
        new(AgentNames.Visualizer, AgentNames.Assembler, "visuals_ready"),
        new(AgentNames.Assembler, AgentNames.HumanReview, "requires_human == true"),
        new(AgentNames.Assembler, AgentNames.Supervisor, "requires_human == false"),
        new(AgentNames.HumanReview, AgentNames.Supervisor, "approved == true"),
        new(AgentNames.HumanReview, AgentNames.Summarizer, "approved == false")
    };

    private static IReadOnlyCollection<WorkflowCheckpoint> BuildCheckpoints() => new List<WorkflowCheckpoint>
    {
        new("post-ingest", "Checkpoint after Reader/Indexer complete", AgentNames.Supervisor, false),
        new("pre-release", "Human approval gate before publishing", AgentNames.HumanReview, true)
    };
}
