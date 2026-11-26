using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Api.Features.Sets.Browse;
using BrickShare.Catalog.Domain;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class BrowseSetsTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Anonymous_visitor_lists_active_sets_only()
    {
        await SetTestData.SaveAsync(Factory, SetTestData.CreateSet("10294"));

        var retired = SetTestData.CreateSet("75192");
        retired.Deactivate();
        await SetTestData.SaveAsync(Factory, retired);

        var page = await GetPageAsync();

        page.Items.Select(item => item.LegoSetNumber).Should().Equal("10294");
    }

    [Fact]
    public async Task Paging_returns_the_requested_page_and_the_total_count()
    {
        await SaveAsync("10001", "Alpha");
        await SaveAsync("10002", "Beta");
        await SaveAsync("10003", "Gamma");

        var page = await GetPageAsync("?page=2&pageSize=2");

        page.Items.Select(item => item.Name).Should().Equal("Gamma");
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task List_returns_400_problem_details_when_page_size_is_above_100()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync("/catalog/sets?pageSize=101", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").TryGetProperty("pageSize", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Theme_filter_returns_only_sets_in_that_theme()
    {
        var starWars = Guid.CreateVersion7();
        await SaveAsync("10001", "Alpha", themeId: starWars);
        await SaveAsync("10002", "Beta", themeId: Guid.CreateVersion7());

        var page = await GetPageAsync($"?themeId={starWars}");

        page.Items.Select(item => item.LegoSetNumber).Should().Equal("10001");
    }

    [Fact]
    public async Task Piece_range_filter_keeps_sets_within_the_range()
    {
        await SaveAsync("10001", "Small", pieces: 300);
        await SaveAsync("10002", "Medium", pieces: 1500);
        await SaveAsync("10003", "Large", pieces: 9000);

        var page = await GetPageAsync("?minPieces=1000&maxPieces=5000");

        page.Items.Select(item => item.LegoSetNumber).Should().Equal("10002");
    }

    [Fact]
    public async Task Price_range_filter_compares_daily_price_in_minor_units()
    {
        await SaveAsync("10001", "Cheap", dailyPrice: 500);
        await SaveAsync("10002", "Mid", dailyPrice: 1500);
        await SaveAsync("10003", "Pricey", dailyPrice: 3000);

        var page = await GetPageAsync("?minPrice=1000&maxPrice=2000");

        page.Items.Select(item => item.LegoSetNumber).Should().Equal("10002");
    }

    [Fact]
    public async Task Sort_by_pieces_orders_by_piece_count()
    {
        await SaveAsync("10001", "Zeta", pieces: 300);
        await SaveAsync("10002", "Alpha", pieces: 9000);
        await SaveAsync("10003", "Mid", pieces: 1500);

        var page = await GetPageAsync("?sort=pieces");

        page.Items.Select(item => item.LegoSetNumber).Should().Equal("10001", "10003", "10002");
    }

    [Fact]
    public async Task Sort_by_price_orders_by_daily_price()
    {
        await SaveAsync("10001", "Zeta", dailyPrice: 500);
        await SaveAsync("10002", "Alpha", dailyPrice: 3000);
        await SaveAsync("10003", "Mid", dailyPrice: 1500);

        var page = await GetPageAsync("?sort=price");

        page.Items.Select(item => item.LegoSetNumber).Should().Equal("10001", "10003", "10002");
    }

    [Fact]
    public async Task Sort_returns_400_problem_details_for_an_unknown_field()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync("/catalog/sets?sort=popularity", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").TryGetProperty("sort", out _).Should().BeTrue();
    }

    private Task<LegoSet> SaveAsync(
        string legoSetNumber, string name, int pieces = 1000, long dailyPrice = 1500, Guid? themeId = null) =>
        SetTestData.SaveAsync(
            Factory,
            SetTestData.CreateSpecifiedSet(legoSetNumber, name, pieces, dailyPrice, themeId ?? Guid.CreateVersion7()));

    private async Task<PagedResponse<SetSummaryResponse>> GetPageAsync(string query = "")
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync($"/catalog/sets{query}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedResponse<SetSummaryResponse>>(
            TestContext.Current.CancellationToken))!;
    }
}