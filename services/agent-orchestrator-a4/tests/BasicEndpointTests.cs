using System.Net;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AgentOrchestratorA4.Api.Tests;

public class BasicEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BasicEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Status_ReturnsOk()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/agent-orchestrator-a4/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Workflow_ReturnsAgentBlueprint()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/agent-orchestrator-a4/workflow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("pls-supervisor-workflow", root.GetProperty("name").GetString());

        var agents = root.GetProperty("agents").EnumerateArray().Select(element => element.GetProperty("name").GetString()).ToList();
        Assert.Contains("reader", agents);
        Assert.Contains("summarizer", agents);
        Assert.Contains("human-review", agents);
    }
}
