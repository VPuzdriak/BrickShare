using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Api.Features.Themes.Facets;
using BrickShare.Catalog.Domain;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class ThemeFacetsTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Theme_facets_count_only_active_sets()
    {
        var starWars = await SaveThemeAsync(158, "Star Wars");
        await SaveSetAsync("10001", starWars.Id);

        var retired = SetTestData.CreateSpecifiedSet("10002", "Retired", 1000, 1500, starWars.Id);
        retired.Deactivate();
        await SetTestData.SaveAsync(Factory, retired);

        var facets = await GetFacetsAsync();

        facets.Single(facet => facet.Id == starWars.Id).SetCount.Should().Be(1);
    }

    [Fact]
    public async Task Theme_without_active_sets_has_a_count_of_zero()
    {
        var starWars = await SaveThemeAsync(158, "Star Wars");

        var facets = await GetFacetsAsync();

        facets.Single(facet => facet.Id == starWars.Id).SetCount.Should().Be(0);
    }

    [Fact]
    public async Task Theme_facets_come_from_the_cache_within_five_minutes()
    {
        var starWars = await SaveThemeAsync(158, "Star Wars");
        await SaveSetAsync("10001", starWars.Id);
        await GetFacetsAsync();

        await SaveSetAsync("10002", starWars.Id);

        var facets = await GetFacetsAsync();

        facets.Single(facet => facet.Id == starWars.Id).SetCount.Should().Be(1);
    }

    private Task<Theme> SaveThemeAsync(int rebrickableThemeId, string name) =>
        SetTestData.SaveThemeAsync(
            Factory, Theme.FromRebrickable(Guid.CreateVersion7(), new RebrickableTheme(rebrickableThemeId, name, null)));

    private Task<LegoSet> SaveSetAsync(string legoSetNumber, Guid themeId) =>
        SetTestData.SaveAsync(
            Factory, SetTestData.CreateSpecifiedSet(legoSetNumber, $"Set {legoSetNumber}", 1000, 1500, themeId));

    private async Task<List<ThemeFacetResponse>> GetFacetsAsync()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync("/catalog/themes", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<List<ThemeFacetResponse>>(
            TestContext.Current.CancellationToken))!;
    }
}