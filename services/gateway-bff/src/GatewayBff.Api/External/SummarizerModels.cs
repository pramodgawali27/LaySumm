namespace GatewayBff.Api.External;

public sealed record RagRetrievalOptions(
    int TopK,
    bool UseHybrid,
    double VectorWeight,
    IReadOnlyList<string>? Keywords,
    IReadOnlyList<string>? SectionAnchors);

public sealed record RagSectionPlan(
    string SectionId,
    string Heading,
    IReadOnlyList<string>? FocusQuestions);

public sealed record RagSummarizationRequest(
    string DocumentId,
    IReadOnlyList<RagSectionPlan> Sections,
    RagRetrievalOptions Retrieval,
    string TargetAudience,
    bool IncludeCitations,
    string? PreferredLanguage,
    double MinimumReadabilityScore);

public sealed record RagCitation(
    string SourceId,
    string Snippet,
    string Uri,
    double Score);

public sealed record RagSectionSummary(
    string SectionId,
    string Heading,
    string Summary,
    double ReadabilityScore,
    IReadOnlyList<RagCitation> Citations);

public sealed record RagSummarizationResponse(
    string SummaryId,
    string DocumentId,
    IReadOnlyList<RagSectionSummary> Sections,
    string ModelVariant,
    DateTimeOffset GeneratedAt,
    double EstimatedCostUsd);

public sealed record RagSummarizationStatusResponse(
    string SummaryId,
    string DocumentId,
    string Status,
    DateTimeOffset LastUpdatedAt);

public interface ISummarizerA3Client
{
    Task<RagSummarizationResponse> CreateSummaryAsync(RagSummarizationRequest request, CancellationToken cancellationToken);
    Task<RagSummarizationStatusResponse?> GetStatusAsync(string summaryId, CancellationToken cancellationToken);
}
