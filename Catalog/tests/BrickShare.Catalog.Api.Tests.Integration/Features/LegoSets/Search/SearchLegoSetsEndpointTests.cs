using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Features.LegoSets;
using BrickShare.Catalog.Api.Features.LegoSets.Add;
using BrickShare.Catalog.Api.Features.LegoSets.Search;
using BrickShare.Catalog.Api.Features.LegoThemes;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;
using BrickShare.Catalog.Api.Shared;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace BrickShare.Catalog.Api.Tests.Integration.Features.LegoSets.Search;

public sealed class SearchLegoSetsEndpointTests(WebApplicationFactory<Program> factory)
  : IClassFixture<WebApplicationFactory<Program>> {
  private readonly HttpClient _client = factory.CreateClient();

  private static Uri SearchUri(string? searchTerm = null, Guid? themeId = null, int page = 1, int pageSize = 10) {
    var url = LegoSetsEndpoints.RoutePrefix + SearchLegoSetsEndpoint.Route;
    var queryParams = new List<string>();
    if (searchTerm is not null) {
      queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");
    }

    if (themeId is not null) {
      queryParams.Add($"themeId={themeId}");
    }

    queryParams.Add($"page={page}");
    queryParams.Add($"pageSize={pageSize}");

    url += "?" + string.Join("&", queryParams);

    return new Uri(url, UriKind.Relative);
  }

  private async Task<Guid> CreateThemeAsync(string name) {
    var response = await _client.PostAsJsonAsync(
      LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route,
      new AddLegoThemeRequest(name));
    var theme = await response.Content.ReadFromJsonAsync<LegoThemeDto>();
    return theme!.Id;
  }

  private async Task CreateLegoSetAsync(string name, string legoId, Guid themeId) {
    var request = new AddLegoSetRequest(name, legoId, new DateOnly(2023, 1, 1), 100, 6, themeId);
    await _client.PostAsJsonAsync(LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);
  }

  [Fact]
  public async Task SearchLegoSets_WithMatchingSearchTerm_ReturnsOkWithMatchingSets() {
    // Arrange
    var technicThemeId = await CreateThemeAsync("Technic");
    var cityThemeId = await CreateThemeAsync("City");
    await CreateLegoSetAsync("Bulldozer 5000", Guid.NewGuid().ToString("N"), technicThemeId);
    await CreateLegoSetAsync("Police Station", Guid.NewGuid().ToString("N"), cityThemeId);

    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri("Bulldozer"));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    PagedResult<FoundLegoSetDto>? result = await response.Content.ReadFromJsonAsync<PagedResult<FoundLegoSetDto>>();
    result.ShouldNotBeNull();
    result.Items.ShouldContain(s => s.Name == "Bulldozer 5000");
    result.Items.ShouldNotContain(s => s.Name == "Police Station");
  }

  [Fact]
  public async Task SearchLegoSets_WithNonMatchingSearchTerm_ReturnsOkWithEmptyList() {
    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri("NonExistingSetXYZ"));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    PagedResult<FoundLegoSetDto>? result = await response.Content.ReadFromJsonAsync<PagedResult<FoundLegoSetDto>>();
    result.ShouldNotBeNull();
    result.Items.ShouldBeEmpty();
  }

  [Fact]
  public async Task SearchLegoSets_WithThemeId_ReturnsOkWithSetsOfThatTheme() {
    // Arrange
    var themeId = await CreateThemeAsync("Ideas");
    await CreateLegoSetAsync("Ideas Globe", Guid.NewGuid().ToString("N"), themeId);

    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri(themeId: themeId));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    PagedResult<FoundLegoSetDto>? result = await response.Content.ReadFromJsonAsync<PagedResult<FoundLegoSetDto>>();
    result.ShouldNotBeNull();
    result.Items.ShouldContain(s => s.Name == "Ideas Globe");
    result.Items.ShouldAllBe(s => s.ThemeName == "Ideas");
  }

  [Fact]
  public async Task SearchLegoSets_WithBothSearchTermAndThemeId_ReturnsOkWithMatchingSets() {
    // Arrange
    var themeId = await CreateThemeAsync("Creator");
    await CreateLegoSetAsync("Creator Car", Guid.NewGuid().ToString("N"), themeId);
    await CreateLegoSetAsync("Creator Boat", Guid.NewGuid().ToString("N"), themeId);

    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri("Creator Car", themeId));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    PagedResult<FoundLegoSetDto>? result = await response.Content.ReadFromJsonAsync<PagedResult<FoundLegoSetDto>>();
    result.ShouldNotBeNull();
    result.Items.ShouldContain(s => s.Name == "Creator Car");
    result.Items.ShouldNotContain(s => s.Name == "Creator Boat");
  }

  [Fact]
  public async Task SearchLegoSets_WithNoParams_ReturnsBadRequest() {
    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri());

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task SearchLegoSets_WithSearchTermShorterThan3Chars_ReturnsBadRequest() {
    // Act
    HttpResponseMessage response = await _client.GetAsync(SearchUri("Te"));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
