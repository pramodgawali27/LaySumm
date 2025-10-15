using System.Collections.Generic;

namespace LaySumm.Api.Processing.Summarization;

public sealed class SummarySection
{
    public required string SectionId { get; init; }

    public required string Title { get; init; }

    public required string PlainLanguageSummary { get; init; }

    public required IReadOnlyList<string> Citations { get; init; }

    public string? WhyItMatters { get; init; }

    public string? Limitations { get; init; }

    public string? Glossary { get; init; }
}
