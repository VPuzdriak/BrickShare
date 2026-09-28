using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.CatalogSets;

public class ThemeTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task Two_sets_from_the_same_theme_share_one_theme_row()
    {
        HttpClient client = Database.Api.CreateClient();

        await Database.CatalogueAsync(client, StockedSet.Titanic);
        await CatalogueConcordeAsync(client);

        await using CatalogDbContext dbContext = Database.NewDbContext();

        Assert.Equal(2, await dbContext.Sets.CountAsync());
        Assert.Equal(1, await dbContext.Themes.CountAsync());

        Assert.Equal(1, await dbContext.Sets
            .Select(set => set.ThemeId)
            .Distinct()
            .CountAsync());
    }

    [Fact]
    public async Task A_catalogued_set_still_reports_its_theme_by_name()
    {
        HttpClient client = Database.Api.CreateClient();

        Guid lookupId = await Database.LookUpAsync(client, StockedSet.Titanic);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets",
            new
            {
                lookupId,
                retailPrice = 629.99m,
                baseRentalPrice = 60.00m,
                minimumRentalDays = 7,
                minimumAge = 18
            });

        CatalogSetResponse? created = await response.Content.ReadFromJsonAsync<CatalogSetResponse>();

        Assert.NotNull(created);
        Assert.Equal("Icons", created.Theme);
    }

    /// <summary>
    /// A second Icons set. The theme id is the same 252 the Titanic came back with, which is the
    /// only thing that makes the first test meaningful.
    /// </summary>
    private async Task CatalogueConcordeAsync(HttpClient client)
    {
        Database.Rebrickable.Sets["10318-1"] = new
        {
            set_num = "10318-1",
            name = "Concorde",
            year = 2023,
            theme_id = 252,
            num_parts = 2083,
            set_img_url = "https://cdn.rebrickable.com/media/sets/10318-1.jpg"
        };

        Database.Rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)null };

        HttpResponseMessage lookup = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10318-1" });

        LookupResponse? draft = await lookup.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets",
            new
            {
                lookupId = draft.LookupId,
                retailPrice = 199.99m,
                baseRentalPrice = 25.00m,
                minimumRentalDays = 7,
                minimumAge = 12
            });

        response.EnsureSuccessStatusCode();
    }
}
