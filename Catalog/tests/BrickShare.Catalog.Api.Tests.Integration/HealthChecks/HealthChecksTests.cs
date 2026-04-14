using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace BrickShare.Catalog.Api.Tests.Integration.HealthChecks;

public sealed class HealthChecksTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HealthChecks_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("Healthy");
    }
}