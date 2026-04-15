using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Features.LegoSets;
using BrickShare.Catalog.Api.Features.LegoSets.Add;
using BrickShare.Catalog.Api.Features.LegoThemes;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace BrickShare.Catalog.Api.Tests.Integration.Features.LegoSets.Retrieve;

public sealed class GetLegoSetEndpointTests(WebApplicationFactory<Program> factory)
  : IClassFixture<WebApplicationFactory<Program>> {
  private readonly HttpClient _client = factory.CreateClient();

  private async Task<Guid> CreateThemeAsync(string name) {
    var response = await _client.PostAsJsonAsync(
      LegoThemesEndpoints.RoutePrefix + AddThemeEndpoint.Route,
      new AddLegoThemeRequest(name));
    var theme = await response.Content.ReadFromJsonAsync<LegoThemeDto>();
    return theme!.Id;
  }

  private async Task<LegoSetDto> CreateLegoSetAsync(string name, string legoId, Guid themeId) {
    var request = new AddLegoSetRequest(name, legoId, new DateOnly(2023, 6, 1), 195, 7, themeId);
    var response = await _client.PostAsJsonAsync(LegoSetsEndpoints.RoutePrefix + AddLegoSetEndpoint.Route, request);
    return (await response.Content.ReadFromJsonAsync<LegoSetDto>())!;
  }

  [Fact]
  public async Task GetLegoSet_WhenSetExists_ReturnsOkWithFullInformation() {
    // Arrange
    var themeId = await CreateThemeAsync("Technic");
    var legoId = Guid.NewGuid().ToString("N");
    var created = await CreateLegoSetAsync("Technic Bulldozer", legoId, themeId);

    // Act
    HttpResponseMessage response = await _client.GetAsync(new Uri($"{LegoSetsEndpoints.RoutePrefix}/{created.Id}", UriKind.Relative));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    LegoSetDto? set = await response.Content.ReadFromJsonAsync<LegoSetDto>();
    set.ShouldNotBeNull();
    set.Id.ShouldBe(created.Id);
    set.Name.ShouldBe("Technic Bulldozer");
    set.LegoId.ShouldBe(legoId);
    set.ReleaseDate.ShouldBe(new DateOnly(2023, 6, 1));
    set.NumberOfParts.ShouldBe(195);
    set.AgeFrom.ShouldBe(7);
    set.ThemeId.ShouldBe(themeId);
    set.ThemeName.ShouldBe("Technic");
  }

  [Fact]
  public async Task GetLegoSet_WhenSetDoesNotExist_ReturnsNotFound() {
    // Arrange
    var nonExistentId = Guid.NewGuid();

    // Act
    HttpResponseMessage response = await _client.GetAsync(new Uri($"{LegoSetsEndpoints.RoutePrefix}/{nonExistentId}", UriKind.Relative));

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }
}



