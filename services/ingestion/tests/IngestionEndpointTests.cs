using System.Net;
using System.Net.Http.Json;
using Ingestion.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Ingestion.Api.Tests;

public class IngestionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IngestionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task PostJob_ReturnsCreated_WhenPayloadValid()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var request = new IngestionRequest(
            DocumentId: "doc-123",
            SourceUri: "https://example.com/doc.pdf",
            FileName: "doc.pdf",
            Locale: "en-US",
            OcrRequired: false,
            Metadata: new Dictionary<string, string> { ["source"] = "unit-test" },
            Priority: 0
        );

        var response = await client.PostAsJsonAsync("/api/ingestion/jobs", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IngestionJobResponse>();
        Assert.NotNull(body);
        Assert.Equal(request.DocumentId, body!.DocumentId);
        Assert.Equal(IngestionStatus.Pending, body.Status);
    }
}
