using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Observability.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public sealed record ObservabilityRequest([Required, StringLength(128)] string ReferenceId, IDictionary<string, string>? Metadata, bool DryRun = false);

public sealed record ObservabilityResponse(string ReferenceId, string Message, DateTimeOffset GeneratedAt);
