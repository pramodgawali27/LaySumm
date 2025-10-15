using System.Collections.Generic;
using System.Linq;
using Platform.AgentFramework.Workflows;

namespace AgentOrchestratorA4.Api.Orchestration;

public sealed record WorkflowBlueprintResponse(
    string Name,
    string Description,
    IReadOnlyList<WorkflowAgentResponse> Agents,
    IReadOnlyList<WorkflowEdgeResponse> Edges,
    IReadOnlyList<WorkflowCheckpointResponse> Checkpoints,
    IReadOnlyDictionary<string, string> Metadata)
{
    public static WorkflowBlueprintResponse FromDescriptor(WorkflowDescriptor descriptor) => new(
        descriptor.Name,
        descriptor.Description,
        descriptor.Agents.Select(name => new WorkflowAgentResponse(name)).ToList(),
        descriptor.Edges.Select(edge => new WorkflowEdgeResponse(edge.From, edge.To, edge.Condition)).ToList(),
        descriptor.Checkpoints.Select(cp => new WorkflowCheckpointResponse(cp.Name, cp.Description, cp.AgentName, cp.RequiresApproval)).ToList(),
        new Dictionary<string, string>(descriptor.Metadata));
}

public sealed record WorkflowAgentResponse(string Name);

public sealed record WorkflowEdgeResponse(string From, string To, string Condition);

public sealed record WorkflowCheckpointResponse(string Name, string Description, string AgentName, bool RequiresApproval);
