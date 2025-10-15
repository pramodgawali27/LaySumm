using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BillingCost.Api.Tests;

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
        var response = await client.GetAsync("/api/billing-cost/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
