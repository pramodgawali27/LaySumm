using System.Threading.Channels;

namespace LaySumm.Api.Services;

public sealed class BackgroundWorkflowQueue
{
    private readonly Channel<WorkflowJob> _channel = Channel.CreateUnbounded<WorkflowJob>();

    public ValueTask QueueAsync(WorkflowJob job, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    public IAsyncEnumerable<WorkflowJob> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
