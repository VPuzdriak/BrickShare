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
