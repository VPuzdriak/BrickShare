using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Features.LegoThemes;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace BrickShare.Catalog.Api.Tests.Integration.Features.LegoThemes.Add;

public sealed class AddThemeEndpointTests(WebApplicationFactory<Program> factory)
  : IClassFixture<WebApplicationFactory<Program>> {
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task AddTheme_WithValidRequest_ReturnsCreated() {
    // Arrange
    var request = new AddLegoThemeRequest("Technic");

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.Created);

    LegoThemeDto? theme = await response.Content.ReadFromJsonAsync<LegoThemeDto>();
    theme.ShouldNotBeNull();
    theme.Name.ShouldBe("Technic");
    theme.Id.ShouldNotBe(Guid.Empty);

    response.Headers.Location.ShouldNotBeNull();
    response.Headers.Location.ToString().ShouldBe($"{LegoThemesEndpoints.RoutePrefix}/{theme.Id}");
  }

  [Fact]
  public async Task AddTheme_WithDuplicateName_ReturnsExistingTheme() {
    // Arrange
    var request = new AddLegoThemeRequest("City");

    // Act
    HttpResponseMessage firstResponse = await _client.PostAsJsonAsync(
      LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route, request);
    HttpResponseMessage secondResponse = await _client.PostAsJsonAsync(
      LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route, request);

    // Assert
    firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    secondResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

    LegoThemeDto? firstTheme = await firstResponse.Content.ReadFromJsonAsync<LegoThemeDto>();
    LegoThemeDto? secondTheme = await secondResponse.Content.ReadFromJsonAsync<LegoThemeDto>();

    firstTheme.ShouldNotBeNull();
    secondTheme.ShouldNotBeNull();
    secondTheme.Id.ShouldBe(firstTheme.Id);
    secondTheme.Name.ShouldBe(firstTheme.Name);
  }
}
