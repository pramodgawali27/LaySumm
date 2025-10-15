using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using SummarizerA3.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SummarizerA3.Api.Tests;

public class RagPipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RagPipelineTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task CreateSummary_ReturnsCreatedResponse()
    {
        using var client = _factory.CreateClient();
        var request = new SummarizationRequest(
            DocumentId: "doc-123",
            Sections: new List<SectionPlan>
            {
                new("section-1", "Findings", new List<string> { "What is the diagnosis?" })
            },
            Retrieval: new RagRetrievalOptions(TopK: 3, UseHybrid: true, VectorWeight: 0.6, Keywords: new List<string> { "diagnosis" }, SectionAnchors: null),
            TargetAudience: "patient",
            IncludeCitations: true,
            PreferredLanguage: "en",
            MinimumReadabilityScore: 60
        );

        var response = await client.PostAsJsonAsync("/api/summarizer-a3/summaries", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SummarizationResponse>();
        Assert.NotNull(body);
        Assert.Equal(request.DocumentId, body!.DocumentId);
        Assert.NotEmpty(body.Sections);
        Assert.True(body.Sections.All(section => section.Citations.Any()));
    }
}
