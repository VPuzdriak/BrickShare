using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Features.LegoSets;
using BrickShare.Catalog.Api.Features.LegoSets.Add;
using BrickShare.Catalog.Api.Features.LegoThemes;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace BrickShare.Catalog.Api.Tests.Integration.Features.LegoSets.Add;

public sealed class AddLegoSetEndpointTests(WebApplicationFactory<Program> factory)
  : IClassFixture<WebApplicationFactory<Program>> {
  private readonly HttpClient _client = factory.CreateClient();

  private async Task<Guid> CreateThemeAsync(string name) {
    var response = await _client.PostAsJsonAsync(
      LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route,
      new AddLegoThemeRequest(name));
    var theme = await response.Content.ReadFromJsonAsync<LegoThemeDto>();
    return theme!.Id;
  }

  [Fact]
  public async Task AddLegoSet_WithValidRequest_ReturnsCreated() {
    // Arrange
    var themeId = await CreateThemeAsync("Technic");
    var request = new AddLegoSetRequest("Technic Bulldozer", Guid.NewGuid().ToString("N"), new DateOnly(2023, 6, 1), 195, 7, themeId);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.Created);

    LegoSetDto? set = await response.Content.ReadFromJsonAsync<LegoSetDto>();
    set.ShouldNotBeNull();
    set.Id.ShouldNotBe(Guid.Empty);
    set.Name.ShouldBe(request.Name);
    set.LegoId.ShouldBe(request.LegoId);
    set.NumberOfParts.ShouldBe(request.NumberOfParts);
    set.AgeFrom.ShouldBe(request.AgeFrom);
    set.ThemeId.ShouldBe(themeId);
    set.ThemeName.ShouldBe("Technic");

    response.Headers.Location.ShouldNotBeNull();
    response.Headers.Location.ToString().ShouldBe($"{LegoSetsEndpoints.RoutePrefix}/{set.Id}");
  }

  [Fact]
  public async Task AddLegoSet_WithDuplicateLegoId_ReturnsConflict() {
    // Arrange
    var themeId = await CreateThemeAsync("City");
    var legoId = Guid.NewGuid().ToString("N");
    var request = new AddLegoSetRequest("City Police Station", legoId, new DateOnly(2022, 1, 1), 668, 6, themeId);

    // Act
    await _client.PostAsJsonAsync(LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);
    HttpResponseMessage secondResponse = await _client.PostAsJsonAsync(
      LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);

    // Assert
    secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task AddLegoSet_WithNonExistentThemeId_ReturnsNotFound() {
    // Arrange
    var request = new AddLegoSetRequest("Unknown Set", Guid.NewGuid().ToString("N"), new DateOnly(2023, 1, 1), 100, 6, Guid.NewGuid());

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task AddLegoSet_WithEmptyName_ReturnsBadRequest() {
    // Arrange
    var themeId = await CreateThemeAsync("Creator");
    var request = new AddLegoSetRequest("", Guid.NewGuid().ToString("N"), new DateOnly(2023, 1, 1), 426, 9, themeId);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task AddLegoSet_WithZeroNumberOfParts_ReturnsBadRequest() {
    // Arrange
    var themeId = await CreateThemeAsync("Star Wars");
    var request = new AddLegoSetRequest("Star Wars Set", Guid.NewGuid().ToString("N"), new DateOnly(2023, 1, 1), 0, 9, themeId);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task AddLegoSet_WithAgeBelowMinimum_ReturnsBadRequest() {
    // Arrange
    var themeId = await CreateThemeAsync("Duplo");
    var request = new AddLegoSetRequest("Duplo Set", Guid.NewGuid().ToString("N"), new DateOnly(2023, 1, 1), 67, 1, themeId);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
