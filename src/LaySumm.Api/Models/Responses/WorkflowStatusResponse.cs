namespace LaySumm.Api.Models.Responses;

public sealed record WorkflowStatusResponse(
    string JobId,
    string State,
    string? Message,
    Uri? ResultJson,
    Uri? ResultHtml,
    Uri? ResultDocx,
    Uri? ResultPdf
);
