using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace GatewayBff.Api.External;

internal sealed class IngestionClient : IIngestionClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IngestionClient> _logger;

    public IngestionClient(HttpClient httpClient, ILogger<IngestionClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IngestionJobResponse> CreateJobAsync(IngestionJobCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/jobs")
        {
            Content = JsonContent.Create(request, options: DefaultJsonOptions.Instance)
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        await response.EnsureSuccessAsync("create ingestion job");

        var payload = await response.Content.ReadFromJsonAsync<IngestionJobResponse>(DefaultJsonOptions.Instance, cancellationToken);
        if (payload is null)
        {
            throw new GatewayHttpException("create ingestion job", HttpStatusCode.InternalServerError, "Empty ingestion response.");
        }

        _logger.LogInformation("Created ingestion job {JobId} for document {DocumentId}", payload.JobId, payload.DocumentId);
        return payload;
    }

    public async Task<IngestionJobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"/api/ingestion/jobs/{jobId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await response.EnsureSuccessAsync("get ingestion job");
        return await response.Content.ReadFromJsonAsync<IngestionJobResponse>(DefaultJsonOptions.Instance, cancellationToken);
    }
}
