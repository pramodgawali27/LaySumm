namespace LaySumm.Api.Services;

public enum WorkflowJobState
{
    Pending,
    Running,
    Succeeded,
    Failed
}

public sealed class WorkflowJobStatus
{
    public required WorkflowJob Job { get; init; }
    public WorkflowJobState State { get; set; }
    public string? Message { get; set; }
    public WorkflowResult? Result { get; set; }
}

public sealed class WorkflowResult
{
    public Uri? JsonUri { get; init; }
    public Uri? HtmlUri { get; init; }
    public Uri? DocxUri { get; init; }
    public Uri? PdfUri { get; init; }
}
