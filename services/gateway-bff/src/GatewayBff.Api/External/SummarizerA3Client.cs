using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace GatewayBff.Api.External;

internal sealed class SummarizerA3Client : ISummarizerA3Client
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SummarizerA3Client> _logger;

    public SummarizerA3Client(HttpClient httpClient, ILogger<SummarizerA3Client> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RagSummarizationResponse> CreateSummaryAsync(RagSummarizationRequest request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/summarizer-a3/summaries", request, DefaultJsonOptions.Instance, cancellationToken);
        await response.EnsureSuccessAsync("create rag summary");

        var payload = await response.Content.ReadFromJsonAsync<RagSummarizationResponse>(DefaultJsonOptions.Instance, cancellationToken);
        if (payload is null)
        {
            throw new GatewayHttpException("create rag summary", HttpStatusCode.InternalServerError, "Empty summarizer response.");
        }

        _logger.LogInformation("Generated summary {SummaryId} for document {DocumentId}", payload.SummaryId, payload.DocumentId);
        return payload;
    }

    public async Task<RagSummarizationStatusResponse?> GetStatusAsync(string summaryId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"/api/summarizer-a3/summaries/{summaryId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await response.EnsureSuccessAsync("get rag summary status");
        return await response.Content.ReadFromJsonAsync<RagSummarizationStatusResponse>(DefaultJsonOptions.Instance, cancellationToken);
    }
}
