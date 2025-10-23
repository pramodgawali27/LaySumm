using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LaySumm.Api.Services;

public sealed class WorkflowDispatcher : BackgroundService
{
    private readonly BackgroundWorkflowQueue _queue;
    private readonly PlainLanguageSummaryOrchestrator _orchestrator;
    private readonly ILogger<WorkflowDispatcher> _logger;

    public WorkflowDispatcher(
        BackgroundWorkflowQueue queue,
        PlainLanguageSummaryOrchestrator orchestrator,
        ILogger<WorkflowDispatcher> logger)
    {
        _queue = queue;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            _orchestrator.Update(job.Id, status =>
            {
                status.State = WorkflowJobState.Running;
                status.Message = "Running summarization workflow.";
            });

            try
            {
                var workflow = _orchestrator.WorkflowBuilder.Build(job);
                var result = await workflow.ExecuteAsync(stoppingToken);

                _orchestrator.Update(job.Id, status =>
                {
                    status.State = WorkflowJobState.Succeeded;
                    status.Message = "Completed successfully.";
                    status.Result = result;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow for job {JobId} failed.", job.Id);
                _orchestrator.Update(job.Id, status =>
                {
                    status.State = WorkflowJobState.Failed;
                    status.Message = ex.Message;
                });
            }
        }
    }
}
