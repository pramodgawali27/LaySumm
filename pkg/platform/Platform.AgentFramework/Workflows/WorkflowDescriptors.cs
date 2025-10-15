using System.Collections.Generic;

namespace Platform.AgentFramework.Workflows;

public sealed record WorkflowEdge(string From, string To, string Condition);

public sealed record WorkflowCheckpoint(string Name, string Description, string AgentName, bool RequiresApproval);

public sealed record WorkflowDescriptor(
    string Name,
    string Description,
    IReadOnlyCollection<string> Agents,
    IReadOnlyCollection<WorkflowEdge> Edges,
    IReadOnlyCollection<WorkflowCheckpoint> Checkpoints,
    IReadOnlyDictionary<string, string> Metadata);
