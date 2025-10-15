using System.ComponentModel.DataAnnotations;

namespace GatewayBff.Api.Contracts;

public sealed record ServiceStatus(string Service, string Version, DateTimeOffset Timestamp);

public enum SummarizationLane
{
    A1,
    A2,
    A3,
    A4
}

public sealed record SectionInput(
    [Required, StringLength(128)] string SectionId,
    [Required, StringLength(256)] string Heading,
    IReadOnlyList<string>? FocusQuestions = null);

public sealed record RetrievalHints(
    [Range(1, 20)] int TopK = 3,
    bool UseHybrid = true,
    [Range(0, 1)] double VectorWeight = 0.5,
    IReadOnlyList<string>? Keywords = null,
    IReadOnlyList<string>? SectionAnchors = null);

public sealed record SummaryDispatchRequest(
    [Required, StringLength(64)] string DocumentId,
    [Required, Url] string SourceUri,
    [Required, StringLength(256)] string Title,
    [Required] SummarizationLane Lane,
    [Required, StringLength(64)] string TargetAudience,
    IReadOnlyDictionary<string, string>? Metadata = null,
    bool OcrRequired = false,
    string? Locale = "en",
    bool RequireHumanReview = false,
    IReadOnlyList<SectionInput>? Sections = null,
    RetrievalHints? Retrieval = null,
    [Range(0, 100)] double MinimumReadabilityScore = 60);

public sealed record CitationView(
    string SourceId,
    string Snippet,
    string Uri,
    double Score);

public sealed record SummarySectionView(
    string SectionId,
    string Heading,
    string Summary,
    double ReadabilityScore,
    IReadOnlyList<CitationView> Citations);

public sealed record SummaryView(
    string SummaryId,
    string ModelVariant,
    DateTimeOffset GeneratedAt,
    double EstimatedCostUsd,
    IReadOnlyList<SummarySectionView> Sections);

public sealed record ValidationView(
    double FleschReadingEase,
    bool MeetsReadability,
    bool NumericConsistencyPassed,
    bool ContainsBannedJargon,
    IReadOnlyList<string> Issues,
    IReadOnlyDictionary<string, string> NumericFindings);

public sealed record SummaryDispatchResponse(
    string DocumentId,
    Guid IngestionJobId,
    SummarizationLane Lane,
    DateTimeOffset SubmittedAt,
    SummaryView? Summary,
    ValidationView? Validation,
    bool SentToHumanReview,
    IReadOnlyDictionary<string, string>? Metadata)
{
    public string? SummaryId => Summary?.SummaryId;
}

public sealed record DocumentStatusView(
    string DocumentId,
    SummarizationLane Lane,
    Guid IngestionJobId,
    string IngestionStatus,
    SummaryView? Summary,
    string? SummaryStatus,
    ValidationView? Validation,
    bool SentToHumanReview,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? LastUpdatedAt,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record BatchGatewayRequest(
    [Required, StringLength(64)] string JobName,
    [Required, MinLength(1)] IReadOnlyList<string> DocumentIds,
    [Required] SummarizationLane Lane,
    DateTimeOffset? ScheduledFor = null,
    [Range(1, 10)] int Priority = 5,
    bool DeduplicateDocuments = true,
    bool EnablePiiRedaction = true);

public enum GatewayBatchStatus
{
    Pending,
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record BatchGatewayResponse(
    Guid JobId,
    string JobName,
    string Lane,
    GatewayBatchStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledFor,
    int Priority,
    bool EnablePiiRedaction,
    IReadOnlyList<string> DocumentIds);
