using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Api.Features.Sets.Details;
using BrickShare.Catalog.Domain;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class GetSetDetailsTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Anonymous_visitor_gets_set_details_with_theme_name_and_prices()
    {
        var themeId = Guid.CreateVersion7();
        await SetTestData.SaveThemeAsync(Factory, Theme.FromRebrickable(themeId, new RebrickableTheme(158, "Star Wars", null)));
        var legoSet = await SetTestData.SaveAsync(
            Factory, SetTestData.CreateSpecifiedSet("10294", "Millennium Falcon", 7541, 1500, themeId));
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync($"/catalog/sets/{legoSet.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var details = await response.Content.ReadFromJsonAsync<SetDetailsResponse>(TestContext.Current.CancellationToken);
        details!.Name.Should().Be("Millennium Falcon");
        details.PieceCount.Should().Be(7541);
        details.ThemeName.Should().Be("Star Wars");
        details.DailyPrice.Should().Be(1500);
        details.Deposit.Should().Be(15000);
        details.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task Details_return_404_problem_details_for_a_deactivated_set()
    {
        var legoSet = SetTestData.CreateSet("10294");
        legoSet.Deactivate();
        await SetTestData.SaveAsync(Factory, legoSet);
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync($"/catalog/sets/{legoSet.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}