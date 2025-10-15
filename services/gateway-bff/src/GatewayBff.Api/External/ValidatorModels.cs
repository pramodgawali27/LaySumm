namespace GatewayBff.Api.External;

public sealed record CitationEvidencePayload(
    string SourceId,
    string Snippet,
    string Uri);

public sealed record ValidationRequestPayload(
    string SummaryId,
    string DocumentId,
    string PlainLanguageSummary,
    IReadOnlyList<CitationEvidencePayload> Citations,
    double MinimumReadabilityScore,
    bool EnforceNumericConsistency);

public sealed record ValidationResponsePayload(
    string SummaryId,
    double FleschReadingEase,
    bool MeetsReadability,
    bool ContainsBannedJargon,
    bool NumericConsistencyPassed,
    IReadOnlyList<string> Issues,
    IReadOnlyDictionary<string, string> NumericFindings);

public interface IValidatorClient
{
    Task<ValidationResponsePayload> ValidateAsync(ValidationRequestPayload request, CancellationToken cancellationToken);
}
