using System.ComponentModel.DataAnnotations;

namespace Ingestion.Api.Contracts;

public enum IngestionStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public sealed record IngestionRequest(
    [Required, StringLength(64)] string DocumentId,
    [Required, Url] string SourceUri,
    [StringLength(256)] string? FileName,
    [StringLength(10)] string? Locale,
    bool OcrRequired,
    IDictionary<string, string>? Metadata,
    [Range(0, 1000)] int Priority = 0
);

public sealed record IngestionJobResponse(
    Guid JobId,
    string DocumentId,
    string SourceUri,
    IngestionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool OcrRequired,
    string? Locale,
    IDictionary<string, string>? Metadata,
    string? LastError
);

public sealed record IngestionRetryRequest(
    [Required] string Reason
);
