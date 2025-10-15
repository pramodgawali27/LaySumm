using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Validator.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Validator.Api.Tests;

public class ValidatorEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ValidatorEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Evaluate_ReturnsValidationResponse()
    {
        using var client = _factory.CreateClient();
        var request = new ValidationRequest(
            SummaryId: "summary-123",
            DocumentId: "doc-123",
            PlainLanguageSummary: "The blood pressure was 120 over 80. The patient felt better.",
            Citations: new List<CitationEvidence>
            {
                new("chunk-1", "Blood pressure recorded as 120/80", "https://retriever/doc/chunk-1")
            },
            MinimumReadabilityScore: 60,
            EnforceNumericConsistency: true
        );

        var response = await client.PostAsJsonAsync("/api/validator/evaluations", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ValidationResponse>();
        Assert.NotNull(body);
        Assert.True(body!.MeetsReadability);
        Assert.True(body.NumericConsistencyPassed);
    }
}
