using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HumanReview.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public sealed record HumanReviewRequest([Required, StringLength(128)] string ReferenceId, IDictionary<string, string>? Metadata, bool DryRun = false);

public sealed record HumanReviewResponse(string ReferenceId, string Message, DateTimeOffset GeneratedAt);
