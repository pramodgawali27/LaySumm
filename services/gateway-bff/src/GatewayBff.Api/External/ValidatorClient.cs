using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace GatewayBff.Api.External;

internal sealed class ValidatorClient : IValidatorClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ValidatorClient> _logger;

    public ValidatorClient(HttpClient httpClient, ILogger<ValidatorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ValidationResponsePayload> ValidateAsync(ValidationRequestPayload request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/validator/evaluations", request, DefaultJsonOptions.Instance, cancellationToken);
        await response.EnsureSuccessAsync("validate summary");

        var payload = await response.Content.ReadFromJsonAsync<ValidationResponsePayload>(DefaultJsonOptions.Instance, cancellationToken);
        if (payload is null)
        {
            throw new GatewayHttpException("validate summary", HttpStatusCode.InternalServerError, "Empty validation response.");
        }

        _logger.LogInformation("Validated summary {SummaryId} with readability {Readability}", payload.SummaryId, payload.FleschReadingEase);
        return payload;
    }
}
