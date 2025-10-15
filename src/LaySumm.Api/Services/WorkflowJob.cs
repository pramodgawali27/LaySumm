using LaySumm.Api.Models.Requests;

namespace LaySumm.Api.Services;

public sealed record WorkflowJob(string Id, PlainLanguageSummaryRequest Request, DateTimeOffset CreatedOn);
