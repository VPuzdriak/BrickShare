using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace BrickShare.Catalog.Api.Tests.Integration.HealthChecks;

public sealed class HealthChecksTests(WebApplicationFactory<Program> factory)
  : IClassFixture<WebApplicationFactory<Program>> {
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task HealthChecks_ReturnsHealthy() {
    HttpResponseMessage response = await _client.GetAsync(new Uri("/health"));
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    string content = await response.Content.ReadAsStringAsync();
    content.ShouldBe("Healthy");
  }
}
