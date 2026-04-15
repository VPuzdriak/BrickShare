using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Features.LegoThemes;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;
using BrickShare.Catalog.Api.Features.LegoThemes.Retrieve;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace BrickShare.Catalog.Api.Tests.Integration.Features.LegoThemes.Retrieve;

public sealed class GetThemesEndpointTests(WebApplicationFactory<Program> factory)
  : IClassFixture<WebApplicationFactory<Program>> {
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task GetThemes_WhenThemesExist_ReturnsOkWithThemes() {
    // Arrange
    var firstRequest = new AddLegoThemeRequest("Technic");
    var secondRequest = new AddLegoThemeRequest("City");

    await _client.PostAsJsonAsync(LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route, firstRequest);
    await _client.PostAsJsonAsync(LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route, secondRequest);

    // Act
    HttpResponseMessage response = await _client.GetAsync(
      new Uri(LegoThemesEndpoints.RoutePrefix + GetThemesEndpoint.Route, UriKind.Relative));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    List<LegoThemeDto>? themes = await response.Content.ReadFromJsonAsync<List<LegoThemeDto>>();
    themes.ShouldNotBeNull();
    themes.Count.ShouldBeGreaterThanOrEqualTo(2);
    themes.ShouldContain(t => t.Name == "Technic");
    themes.ShouldContain(t => t.Name == "City");
  }
}
