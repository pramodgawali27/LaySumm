using BatchOrchestratorA5.Api.Contracts;

namespace GatewayBff.Api.External;

public sealed record BatchJobCreateRequest(
    string JobName,
    IReadOnlyList<string> DocumentIds,
    string Lane,
    DateTimeOffset? ScheduledFor,
    int Priority,
    bool DeduplicateDocuments,
    bool EnablePiiRedaction);

public sealed record BatchJobResponsePayload(
    Guid JobId,
    string JobName,
    IReadOnlyList<string> DocumentIds,
    string Lane,
    BatchJobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledFor,
    int Priority,
    bool EnablePiiRedaction,
    string? FailureReason);

public interface IBatchClient
{
    Task<BatchJobResponsePayload> CreateAsync(BatchJobCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken);
}
