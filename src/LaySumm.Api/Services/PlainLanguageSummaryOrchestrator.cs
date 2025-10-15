using System.Collections.Generic;
using LaySumm.Api.Models.Requests;
using LaySumm.Api.Models.Responses;
using Microsoft.Extensions.Logging;

namespace LaySumm.Api.Services;

public sealed class PlainLanguageSummaryOrchestrator
{
    private readonly BackgroundWorkflowQueue _queue;
    private readonly WorkflowBuilder _workflowBuilder;
    private readonly ILogger<PlainLanguageSummaryOrchestrator> _logger;
    private readonly Dictionary<string, WorkflowJobStatus> _store = new();
    private readonly object _lock = new();

    public PlainLanguageSummaryOrchestrator(
        BackgroundWorkflowQueue queue,
        WorkflowBuilder workflowBuilder,
        ILogger<PlainLanguageSummaryOrchestrator> logger)
    {
        _queue = queue;
        _workflowBuilder = workflowBuilder;
        _logger = logger;
    }

    public Task<WorkflowStatusResponse?> GetStatusAsync(string jobId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _store.TryGetValue(jobId, out var status);
            return Task.FromResult(status is null ? null : MapToResponse(status));
        }
    }

    internal WorkflowBuilder WorkflowBuilder => _workflowBuilder;

    public async Task<WorkflowJob> EnqueueAsync(PlainLanguageSummaryRequest request, CancellationToken cancellationToken)
    {
        var job = new WorkflowJob(Guid.NewGuid().ToString("N"), request, DateTimeOffset.UtcNow);
        var status = new WorkflowJobStatus
        {
            Job = job,
            State = WorkflowJobState.Pending
        };

        lock (_lock)
        {
            _store[job.Id] = status;
        }

        await _queue.QueueAsync(job, cancellationToken);
        _logger.LogInformation("Enqueued PLS job {JobId} with {DocumentCount} documents.", job.Id, request.Documents.Count);
        return job;
    }

    public void Update(string jobId, Action<WorkflowJobStatus> updater)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(jobId, out var status))
            {
                updater(status);
            }
        }
    }

    public WorkflowStatusResponse MapToResponse(WorkflowJobStatus status)
    {
        return new WorkflowStatusResponse(
            status.Job.Id,
            status.State.ToString(),
            status.Message,
            status.Result?.JsonUri,
            status.Result?.HtmlUri,
            status.Result?.DocxUri,
            status.Result?.PdfUri);
    }
}
