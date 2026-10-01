using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Browse;

public class SetDetailTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_set_with_no_copies_shows_in_full_with_no_price()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        Assert.Equal(setId, detail.Id);
        Assert.Equal("10294-1", detail.SetNumber);
        Assert.Equal("Titanic", detail.Name);
        Assert.Equal("Icons", detail.Theme);
        Assert.Equal(9092, detail.PieceCount);
        Assert.Equal(7, detail.MinimumRentalDays);
        Assert.Equal(0, detail.AvailableCount);

        // Not 0.00, for the reason episode 34 gave: zero is a claim that the set is free.
        Assert.Null(detail.StartingPrice);
        Assert.Empty(detail.Copies);
    }

    [Fact]
    public async Task A_set_nobody_catalogued_is_a_404_that_says_so()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync($"/api/v1/catalog/sets/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("not in the catalog", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Every_copy_is_listed_including_one_out_on_rent()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        Guid excellent = await Database.RegisterCopyAsync(client, setId, "Excellent");
        Guid fair = await Database.RegisterCopyAsync(client, setId, "Fair");
        await SendOnRentAsync(fair);

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        Assert.Equal(1, detail.AvailableCount);
        Assert.Equal(2, detail.Copies.Count);
        Assert.True(detail.Copies.Single(copy => copy.Id == excellent).Available);

        // The copy a customer subscribes to (UC-7.5). Filter it out and the subscribe button has
        // nothing to point at.
        Assert.False(detail.Copies.Single(copy => copy.Id == fair).Available);
    }

    // tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — paste directly above it
    [Fact]
    public async Task Each_copy_carries_its_own_price_and_deposit_cheapest_first()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        await Database.RegisterCopyAsync(client, setId, "Excellent");
        await Database.RegisterCopyAsync(client, setId, "Fair");

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        // 60.00 base and 629.99 retail, times 0.55 for Fair and 0.85 for Excellent. 346.4945 and
        // 535.4915 round to the cent the way Money does. Registered Excellent first, listed Fair first.
        Assert.Equal(
            [(ConditionGrade.Fair, 33.00m, 346.49m), (ConditionGrade.Excellent, 51.00m, 535.49m)],
            detail.Copies.Select(copy => (copy.Grade, copy.RentalPrice, copy.Deposit)));
    }

    [Fact]
    public async Task The_starting_price_is_the_price_of_the_cheapest_copy_a_customer_can_reserve()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        await Database.RegisterCopyAsync(client, setId, "Excellent");
        Guid fair = await Database.RegisterCopyAsync(client, setId, "Fair");
        await SendOnRentAsync(fair);

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        SetCopyResponse cheapestAvailable = detail.Copies
            .Where(copy => copy.Available)
            .MinBy(copy => copy.RentalPrice)!;

        // Two sources for the same number: the view's SQL and PriceCalculator's C#. If they ever
        // disagree, a customer is shown one price in the list and another on this page.
        Assert.Equal(51.00m, detail.StartingPrice);
        Assert.Equal(cheapestAvailable.RentalPrice, detail.StartingPrice);
    }

    // tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — paste directly above it
    [Fact]
    public async Task A_retired_copy_is_not_on_show()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        Guid kept = await Database.RegisterCopyAsync(client, setId, "New");
        Guid retired = await Database.RegisterCopyAsync(client, setId, "Fair");

        HttpResponseMessage retirement = await client.PostAsync(
            $"/api/v1/catalog/copies/{retired}/retirement", content: null);

        retirement.EnsureSuccessStatusCode();

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        Assert.Equal([kept], detail.Copies.Select(copy => copy.Id));
    }

    private async Task<SetDetailResponse> GetDetailAsync(HttpClient client, Guid setId)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/v1/catalog/sets/{setId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        SetDetailResponse? detail =
            await response.Content.ReadFromJsonAsync<SetDetailResponse>(Database.Api.Json);

        Assert.NotNull(detail);

        return detail;
    }

    /// <summary>
    /// Drives a copy to OnRent through the domain rather than through HTTP, because the endpoints
    /// that would do it belong to a rentals service that does not exist yet.
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
