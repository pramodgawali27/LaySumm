using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgentOrchestratorA4.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public sealed record AgentOrchestratorA4Request([Required, StringLength(128)] string ReferenceId, IDictionary<string, string>? Metadata, bool DryRun = false);

public sealed record AgentOrchestratorA4Response(string ReferenceId, string Message, DateTimeOffset GeneratedAt);
