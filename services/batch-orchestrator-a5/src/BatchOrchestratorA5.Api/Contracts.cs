using System.ComponentModel.DataAnnotations;

namespace BatchOrchestratorA5.Api.Contracts;

public enum BatchJobStatus
{
    Pending,
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record BatchJobRequest(
    [Required, StringLength(64)] string JobName,
    [Required, MinLength(1)] IReadOnlyList<string> DocumentIds,
    [Required, StringLength(16)] string Lane,
    DateTimeOffset? ScheduledFor,
    [Range(1, 10)] int Priority = 5,
    bool DeduplicateDocuments = true,
    bool EnablePiiRedaction = true
);

public sealed record BatchJobResponse(
    Guid JobId,
    string JobName,
    IReadOnlyList<string> DocumentIds,
    string Lane,
    BatchJobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledFor,
    int Priority,
    bool EnablePiiRedaction,
    string? FailureReason
);

public sealed record BatchJobStatusResponse(
    Guid JobId,
    BatchJobStatus Status,
    DateTimeOffset UpdatedAt,
    int CompletedCount,
    int FailedCount
);

public sealed record BatchJobCancelRequest(
    [Required] string Reason
);
