using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace GatewayBff.Api.External;

internal sealed class BatchClient : IBatchClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BatchClient> _logger;

    public BatchClient(HttpClient httpClient, ILogger<BatchClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BatchJobResponsePayload> CreateAsync(BatchJobCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/batch/jobs")
        {
            Content = JsonContent.Create(request, options: DefaultJsonOptions.Instance)
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        await response.EnsureSuccessAsync("create batch job");

        var payload = await response.Content.ReadFromJsonAsync<BatchJobResponsePayload>(DefaultJsonOptions.Instance, cancellationToken);
        if (payload is null)
        {
            throw new GatewayHttpException("create batch job", HttpStatusCode.InternalServerError, "Empty batch response.");
        }

        _logger.LogInformation("Created batch job {JobId} targeting lane {Lane}", payload.JobId, payload.Lane);
        return payload;
    }
}
