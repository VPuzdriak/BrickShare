using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BrickShare.Catalog.IntegrationTests.HealthChecks;

public class LivenessTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Live_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
