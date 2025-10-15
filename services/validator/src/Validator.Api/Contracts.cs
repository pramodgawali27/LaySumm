using System.ComponentModel.DataAnnotations;

namespace Validator.Api.Contracts;

public sealed record CitationEvidence(
    [Required] string SourceId,
    [Required] string Snippet,
    [Required] string Uri
);

public sealed record ValidationRequest(
    [Required, StringLength(64)] string SummaryId,
    [Required] string DocumentId,
    [Required] string PlainLanguageSummary,
    [Required] IReadOnlyList<CitationEvidence> Citations,
    double MinimumReadabilityScore = 60,
    bool EnforceNumericConsistency = true
);

public sealed record ValidationResponse(
    string SummaryId,
    double FleschReadingEase,
    bool MeetsReadability,
    bool ContainsBannedJargon,
    bool NumericConsistencyPassed,
    IReadOnlyList<string> Issues,
    IReadOnlyDictionary<string, string> NumericFindings
);

public sealed record ReadabilityPreviewRequest(
    [Required] string Text
);

public sealed record ReadabilityPreviewResponse(
    double FleschReadingEase,
    double GradeLevel
);
