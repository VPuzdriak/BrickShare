using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

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

    private async Task<SetListingResponse> GetAvailableSetsAsync(HttpClient client)
    {
        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);

        return Assert.Single(page.Sets);
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
