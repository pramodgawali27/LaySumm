using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Retriever.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public sealed record RetrieverRequest([Required, StringLength(128)] string ReferenceId, IDictionary<string, string>? Metadata, bool DryRun = false);

public sealed record RetrieverResponse(string ReferenceId, string Message, DateTimeOffset GeneratedAt);
