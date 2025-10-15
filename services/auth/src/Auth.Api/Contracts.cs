using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public sealed record AuthRequest([Required, StringLength(128)] string ReferenceId, IDictionary<string, string>? Metadata, bool DryRun = false);

public sealed record AuthResponse(string ReferenceId, string Message, DateTimeOffset GeneratedAt);
