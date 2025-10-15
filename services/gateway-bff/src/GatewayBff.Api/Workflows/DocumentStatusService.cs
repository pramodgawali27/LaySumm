using GatewayBff.Api.Contracts;
using GatewayBff.Api.External;

namespace GatewayBff.Api.Workflows;

internal sealed class DocumentStatusService
{
    private readonly IDocumentStateStore _stateStore;
    private readonly IIngestionClient _ingestionClient;
    private readonly ISummarizerA3Client _summarizerClient;

    public DocumentStatusService(
        IDocumentStateStore stateStore,
        IIngestionClient ingestionClient,
        ISummarizerA3Client summarizerClient)
    {
        _stateStore = stateStore;
        _ingestionClient = ingestionClient;
        _summarizerClient = summarizerClient;
    }

    public async Task<DocumentStatusView> GetAsync(string documentId, CancellationToken cancellationToken)
    {
        if (!_stateStore.TryGet(documentId, out var response))
        {
            throw new KeyNotFoundException($"Document {documentId} not found.");
        }

        var ingestion = await _ingestionClient.GetAsync(response.IngestionJobId, cancellationToken);
        if (ingestion is null)
        {
            throw new GatewayHttpException("fetch ingestion job", System.Net.HttpStatusCode.NotFound, $"Ingestion job {response.IngestionJobId} not found.");
        }

        string? summaryStatus = null;
        DateTimeOffset? summaryUpdated = null;
        if (response.Summary?.SummaryId is { } summaryId)
        {
            var status = await _summarizerClient.GetStatusAsync(summaryId, cancellationToken);
            summaryStatus = status?.Status ?? "unknown";
            summaryUpdated = status?.LastUpdatedAt;
        }

        var timestamps = new List<DateTimeOffset> { response.SubmittedAt, ingestion.UpdatedAt };
        if (summaryUpdated.HasValue)
        {
            timestamps.Add(summaryUpdated.Value);
        }

        var lastUpdated = timestamps.Count > 0
            ? timestamps.Max()
            : response.SubmittedAt;

        return new DocumentStatusView(
            response.DocumentId,
            response.Lane,
            response.IngestionJobId,
            ingestion.Status.ToString(),
            response.Summary,
            summaryStatus,
            response.Validation,
            response.SentToHumanReview,
            response.SubmittedAt,
            lastUpdated,
            response.Metadata);
    }
}
