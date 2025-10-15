using System.Collections.Generic;

namespace Platform.AgentFramework.Agents;

/// <summary>
/// Describes an agent role within the LaySumm supervisor workflow including instructions and tool access.
/// </summary>
public sealed record AgentBlueprint(
    string Name,
    string Description,
    string Instructions,
    IReadOnlyList<string> ToolNames,
    IReadOnlyDictionary<string, string> ContextProviders,
    bool RequiresThread = true);
