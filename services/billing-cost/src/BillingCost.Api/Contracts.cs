using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BillingCost.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public sealed record BillingCostRequest([Required, StringLength(128)] string ReferenceId, IDictionary<string, string>? Metadata, bool DryRun = false);

public sealed record BillingCostResponse(string ReferenceId, string Message, DateTimeOffset GeneratedAt);
