using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PiiRedactor.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public sealed record PiiRedactorRequest([Required, StringLength(128)] string ReferenceId, IDictionary<string, string>? Metadata, bool DryRun = false);

public sealed record PiiRedactorResponse(string ReferenceId, string Message, DateTimeOffset GeneratedAt);
