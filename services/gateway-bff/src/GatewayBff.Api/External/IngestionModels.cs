namespace GatewayBff.Api.External;

public enum IngestionJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public sealed record IngestionJobCreateRequest(
    string DocumentId,
    string SourceUri,
    string? FileName,
    string? Locale,
    bool OcrRequired,
    IDictionary<string, string> Metadata,
    int Priority);

public sealed record IngestionJobResponse(
    Guid JobId,
    string DocumentId,
    string SourceUri,
    IngestionJobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool OcrRequired,
    string? Locale,
    IDictionary<string, string>? Metadata,
    string? LastError);

public interface IIngestionClient
{
    Task<IngestionJobResponse> CreateJobAsync(IngestionJobCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken);
    Task<IngestionJobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken);
}
