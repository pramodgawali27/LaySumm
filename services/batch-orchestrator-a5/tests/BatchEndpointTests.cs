using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using BatchOrchestratorA5.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BatchOrchestratorA5.Api.Tests;

public class BatchEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BatchEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task CreateBatchJob_ReturnsCreated()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var request = new BatchJobRequest(
            JobName: "daily-medical-summaries",
            DocumentIds: new List<string> { "doc-1", "doc-2", "doc-1" },
            Lane: "A5",
            ScheduledFor: DateTimeOffset.UtcNow.AddMinutes(5),
            Priority: 4,
            DeduplicateDocuments: true,
            EnablePiiRedaction: true
        );

        var response = await client.PostAsJsonAsync("/api/batch/jobs", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BatchJobResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.DocumentIds.Count);
        Assert.Equal(BatchJobStatus.Scheduled, body.Status);
    }
}
