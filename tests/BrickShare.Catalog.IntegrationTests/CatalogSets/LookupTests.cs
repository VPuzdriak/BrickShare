using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.CatalogSets;

public class LookupTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_lookup_returns_the_facts_staff_do_not_type()
    {
        Database.Rebrickable.Sets["10294-1"] = Titanic();
        Database.Rebrickable.Themes[252] = Icons();

        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);
        Assert.NotEqual(Guid.Empty, draft.LookupId);
        Assert.Equal("Titanic", draft.Name);
        Assert.Equal("Icons", draft.Theme);
        Assert.Equal(9092, draft.PieceCount);
    }

    [Fact]
    public async Task A_set_Rebrickable_has_never_heard_of_is_not_found_rather_than_broken()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "99999-9" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_malformed_set_number_never_reaches_Rebrickable()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, Database.Rebrickable.Requests);
    }

    [Fact]
    public async Task Looking_the_same_set_up_twice_is_two_snapshots()
    {
        Database.Rebrickable.Sets["10294-1"] = Titanic();
        Database.Rebrickable.Themes[252] = Icons();

        HttpClient client = Database.Api.CreateClient();

        await client.PostAsJsonAsync("/api/v1/catalog/lookups", new { setNumber = "10294-1" });
        await client.PostAsJsonAsync("/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(2, await dbContext.Snapshots.CountAsync());
    }

    // tests/BrickShare.Catalog.IntegrationTests/CatalogSets/LookupTests.cs — add above the private Titanic helper
    [Fact]
    public async Task A_nested_theme_is_stored_as_the_one_customers_browse_by()
    {
        Database.Rebrickable.Sets["75192-1"] = MillenniumFalcon();
        Database.Rebrickable.Themes[171] =
            new { id = 171, name = "Ultimate Collector Series", parent_id = (int?)158 };
        Database.Rebrickable.Themes[158] = new { id = 158, name = "Star Wars", parent_id = (int?)null };

        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "75192-1" });

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);
        Assert.Equal("Star Wars", draft.Theme);
    }

    [Fact]
    public async Task A_top_level_theme_costs_one_call_and_not_two()
    {
        Database.Rebrickable.Sets["10294-1"] = Titanic();
        Database.Rebrickable.Themes[252] = Icons();

        HttpClient client = Database.Api.CreateClient();

        await client.PostAsJsonAsync("/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        // One for the set, one for its theme. A parentless theme must not provoke a third.
        Assert.Equal(2, Database.Rebrickable.Requests);
    }

    [Fact]
    public async Task A_cycle_in_the_theme_tree_does_not_hang_the_lookup()
    {
        Database.Rebrickable.Sets["10294-1"] = Titanic();
        Database.Rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)900 };
        Database.Rebrickable.Themes[900] = new { id = 900, name = "Loop", parent_id = (int?)252 };

        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static object MillenniumFalcon() => new
    {
        set_num = "75192-1",
        name = "Millennium Falcon",
        year = 2017,
        theme_id = 171,
        num_parts = 7541,
        set_img_url = "https://cdn.rebrickable.com/media/sets/75192-1.jpg"
    };

    private static object Titanic() => new
    {
        set_num = "10294-1",
        name = "Titanic",
        year = 2021,
        theme_id = 252,
        num_parts = 9092,
        set_img_url = "https://cdn.rebrickable.com/media/sets/10294-1.jpg"
    };

    private static object Icons() => new { id = 252, name = "Icons", parent_id = (int?)null };
}
