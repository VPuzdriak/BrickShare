using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Browse;

public class BrowseSetsTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_set_with_no_copies_still_shows_in_full()
    {
        HttpClient client = Database.Api.CreateClient();
        var setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);

        SetListingResponse titanic = Assert.Single(page.Sets);

        Assert.Equal(setId, titanic.Id);
        Assert.Equal("Titanic", titanic.Name);
        Assert.Equal("Icons", titanic.Theme);
        Assert.Equal(0, titanic.AvailableCount);

        // Not 0.00. A price of zero is a claim that the set is free; there is no price at all.
        Assert.Null(titanic.StartingPrice);
    }

    // tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — a second [Fact], below the first
    [Fact]
    public async Task The_starting_price_is_the_cheapest_copy_that_is_available()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        await Database.RegisterCopyAsync(client, setId, "Excellent");
        Guid fair = await Database.RegisterCopyAsync(client, setId, "Fair");

        SetListingResponse both = await GetAvailableSetsAsync(client);

        // 60.00 × 0.55 — the same number PriceCalculator.RentalPrice gives for a Fair copy.
        Assert.Equal(2, both.AvailableCount);
        Assert.Equal(33.00m, both.StartingPrice);

        await SendOnRentAsync(fair);

        SetListingResponse onlyExcellent = await GetAvailableSetsAsync(client);

        // The Fair copy is still cheaper. It is also in somebody's living room.
        Assert.Equal(1, onlyExcellent.AvailableCount);
        Assert.Equal(51.00m, onlyExcellent.StartingPrice);
    }

    [Theory]
    [InlineData("", new[] { "10318-1", "41704-1", "10294-1" })]
    [InlineData("?minPieces=2000", new[] { "10318-1", "10294-1" })]
    [InlineData("?maxPieces=2083", new[] { "10318-1", "41704-1" })]
    [InlineData("?age=8", new[] { "41704-1" })]
    [InlineData("?availableNow=true", new[] { "10318-1", "10294-1" })]
    [InlineData("?maxPrice=20", new[] { "10318-1" })]
    [InlineData("?maxPrice=100", new[] { "10318-1", "10294-1" })]
    [InlineData("?age=18&minPieces=5000", new[] { "10294-1" })]
    [InlineData("?limit=1", new[] { "10318-1" })]
    public async Task Filters_narrow_the_catalog_and_the_order_is_by_name(
        string query, string[] expectedSetNumbers)
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets{query}", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal(expectedSetNumbers, page.Sets.Select(set => set.SetNumber));
    }

    [Fact]
    public async Task Filtering_by_theme_uses_the_id_from_the_filter_list()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        ThemesResponse? themes =
            await client.GetFromJsonAsync<ThemesResponse>("/api/v1/catalog/themes", Database.Api.Json);

        Assert.NotNull(themes);

        Guid icons = themes.Themes.Single(theme => theme.Name == "Icons").Id;

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?themeId={icons}", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal(["10318-1", "10294-1"], page.Sets.Select(set => set.SetNumber));
    }

    [Theory]
    [InlineData("?minPieces=3000&maxPieces=2000", "maxPieces")]
    [InlineData("?limit=51", "limit")]
    [InlineData("?age=19", "age")]
    [InlineData("?maxPrice=-1", "maxPrice")]
    public async Task A_filter_that_cannot_match_anything_is_refused(string query, string field)
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/catalog/sets{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(field, problem.Errors.Keys);
    }

    [Fact]
    public async Task Every_theme_on_the_shelf_says_how_many_sets_it_holds()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal([("Friends", 1), ("Icons", 2)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }

    [Fact]
    public async Task A_theme_with_nothing_left_stays_in_the_list_with_zero()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            "/api/v1/catalog/sets?maxPrice=20", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal([("Friends", 0), ("Icons", 1)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }

    [Fact]
    public async Task A_theme_count_obeys_every_filter_except_the_theme()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);
        Guid icons = await GetThemeIdFromSetsAsync(client, "Icons");

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?themeId={icons}&maxPieces=2083", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal(["10318-1"], page.Sets.Select(set => set.SetNumber));

        // Friends has one set under 2,083 pieces. The customer is looking at Icons, and still has
        // to be told that, or they could never find their way across.
        Assert.Equal([("Friends", 1), ("Icons", 1)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("&maxPrice=20")]
    [InlineData("&age=8")]
    [InlineData("&availableNow=true&maxPieces=5000")]
    public async Task A_theme_count_is_the_number_of_sets_choosing_that_theme_returns(string otherFilters)
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? counted = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?limit=50{otherFilters}", Database.Api.Json);

        Assert.NotNull(counted);

        foreach (ThemeFacetResponse theme in counted.Themes)
        {
            BrowseSetsResponse? chosen = await client.GetFromJsonAsync<BrowseSetsResponse>(
                $"/api/v1/catalog/sets?limit=50&themeId={theme.Id}{otherFilters}", Database.Api.Json);

            Assert.NotNull(chosen);
            Assert.Equal(theme.SetCount, chosen.Sets.Count);
        }
    }

    [Fact]
    public async Task The_counts_are_of_the_catalog_not_of_the_page()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            "/api/v1/catalog/sets?limit=1", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Single(page.Sets);
        Assert.Equal([("Friends", 1), ("Icons", 2)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }


    private async Task StockTheShelfAsync(HttpClient client)
    {
        Guid titanic = await Database.CatalogueAsync(client, StockedSet.Titanic);
        Guid concorde = await Database.CatalogueAsync(client, StockedSet.Concorde);
        await Database.CatalogueAsync(client, StockedSet.MainStreetBuilding);

        await Database.RegisterCopyAsync(client, titanic, "New");
        await Database.RegisterCopyAsync(client, concorde, "Fair");
    }

    private async Task<SetListingResponse> GetAvailableSetsAsync(HttpClient client)
    {
        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);

        return Assert.Single(page.Sets);
    }

    private async Task<Guid> GetThemeIdFromSetsAsync(HttpClient client, string name)
    {
        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);

        return page.Themes.Single(theme => theme.Name == name).Id;
    }


    /// <summary>
    /// The same helper as RetireCopyTests, for the same reason: the rentals service that makes this
    /// transition does not exist. Twice is a coincidence; the third caller moves it to CatalogFlow.
    /// </summary>
    private async Task SendOnRentAsync(Guid copyId)
    {
        await using CatalogDbContext dbContext = Database.NewDbContext();
        Copy copy = await dbContext.Copies.SingleAsync(candidate => candidate.Id == copyId);

        copy.Reserve();
        copy.Collect();

        await dbContext.SaveChangesAsync();
    }
}
