using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SummarizerA2.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public sealed record SummarizerA2Request([Required, StringLength(128)] string ReferenceId, IDictionary<string, string>? Metadata, bool DryRun = false);

public sealed record SummarizerA2Response(string ReferenceId, string Message, DateTimeOffset GeneratedAt);
