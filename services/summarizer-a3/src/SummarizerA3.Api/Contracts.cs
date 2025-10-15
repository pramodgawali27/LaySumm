using System.ComponentModel.DataAnnotations;

namespace SummarizerA3.Api.Contracts;

public sealed record RagRetrievalOptions(
    [Range(1, 20)] int TopK,
    bool UseHybrid,
    [Range(0, 1)] double VectorWeight,
    IReadOnlyList<string>? Keywords,
    IReadOnlyList<string>? SectionAnchors
);

public sealed record SectionPlan(
    [Required, StringLength(128)] string SectionId,
    [Required, StringLength(256)] string Heading,
    IReadOnlyList<string>? FocusQuestions
);

public sealed record SummarizationRequest(
    [Required, StringLength(64)] string DocumentId,
    [Required, MinLength(1)] IReadOnlyList<SectionPlan> Sections,
    [Required] RagRetrievalOptions Retrieval,
    [Required, StringLength(64)] string TargetAudience,
    bool IncludeCitations = true,
    string? PreferredLanguage = "en",
    double MinimumReadabilityScore = 60
);

public sealed record Citation(
    [Required] string SourceId,
    [Required] string Snippet,
    [Required] string Uri,
    double Score
);

public sealed record SectionSummary(
    string SectionId,
    string Heading,
    string Summary,
    double ReadabilityScore,
    IReadOnlyList<Citation> Citations
);

public sealed record SummarizationResponse(
    string SummaryId,
    string DocumentId,
    IReadOnlyList<SectionSummary> Sections,
    string ModelVariant,
    DateTimeOffset GeneratedAt,
    double EstimatedCostUsd
);

public sealed record SummarizationStatusResponse(
    string SummaryId,
    string DocumentId,
    string Status,
    DateTimeOffset LastUpdatedAt
);
