using System.ComponentModel.DataAnnotations;

namespace LaySumm.Api.Models.Requests;

public sealed class PlainLanguageSummaryRequest
{
    [Required]
    public required IReadOnlyList<SourceDocument> Documents { get; init; }

    public string? UserPrompt { get; init; }

    public bool ForceRegenerate { get; init; }
}

public sealed class SourceDocument
{
    [Required]
    [Url]
    public required string BlobUri { get; init; }

    [Required]
    public required string FileName { get; init; }

    public string? MediaType { get; init; }
}
