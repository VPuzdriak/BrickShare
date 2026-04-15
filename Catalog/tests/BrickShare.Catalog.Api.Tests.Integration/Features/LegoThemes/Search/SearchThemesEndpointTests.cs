using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Features.LegoThemes;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;
using BrickShare.Catalog.Api.Features.LegoThemes.Search;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace BrickShare.Catalog.Api.Tests.Integration.Features.LegoThemes.Search;

public sealed class SearchThemesEndpointTests(WebApplicationFactory<Program> factory)
  : IClassFixture<WebApplicationFactory<Program>> {
  private readonly HttpClient _client = factory.CreateClient();

  private static Uri SearchUri(string? searchTerm = null) {
    var url = LegoThemesEndpoints.RoutePrefix + SearchThemesEndpoint.Route;
    if (searchTerm is not null) {
      url += $"?searchTerm={Uri.EscapeDataString(searchTerm)}";
    }
    return new Uri(url, UriKind.Relative);
  }

  [Fact]
  public async Task SearchThemes_WithMatchingTerm_ReturnsOkWithMatchingThemes() {
    // Arrange
    await _client.PostAsJsonAsync(LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route,
      new AddLegoThemeRequest("Technic"));
    await _client.PostAsJsonAsync(LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route,
      new AddLegoThemeRequest("City"));

    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri("Tec"));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    List<LegoThemeDto>? themes = await response.Content.ReadFromJsonAsync<List<LegoThemeDto>>();
    themes.ShouldNotBeNull();
    themes.ShouldContain(t => t.Name == "Technic");
    themes.ShouldNotContain(t => t.Name == "City");
  }

  [Fact]
  public async Task SearchThemes_WithNonMatchingTerm_ReturnsOkWithEmptyList() {
    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri("NonExistingTerm"));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    List<LegoThemeDto>? themes = await response.Content.ReadFromJsonAsync<List<LegoThemeDto>>();
    themes.ShouldNotBeNull();
    themes.ShouldBeEmpty();
  }

  [Fact]
  public async Task SearchThemes_WithNoSearchTerm_ReturnsBadRequest() {
    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri());

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task SearchThemes_WithSearchTermShorterThan3Chars_ReturnsBadRequest() {
    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri("Te"));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
