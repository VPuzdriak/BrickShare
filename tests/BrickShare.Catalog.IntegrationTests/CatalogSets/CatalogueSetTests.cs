using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.CatalogSets;

public class CatalogueSetTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_catalogued_set_comes_back_created()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid lookupId = await LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task An_empty_request_is_refused_field_by_field()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("lookupId", problem.Errors.Keys);
        Assert.Contains("minimumRentalDays", problem.Errors.Keys);
        Assert.DoesNotContain("setNumber", problem.Errors.Keys);
        Assert.DoesNotContain("name", problem.Errors.Keys);
    }

    [Fact]
    public async Task A_minimum_rental_period_the_shop_cannot_honour_is_refused()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid lookupId = await LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId, minimumRentalDays: 30));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("28", problem.Detail);
    }

    [Fact]
    public async Task A_set_that_is_already_catalogued_cannot_be_catalogued_again()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid lookupId = await LookUpTitanicAsync(client);

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(1, await dbContext.Sets.CountAsync());
    }

    [Fact]
    public async Task A_lookup_that_was_never_issued_cannot_be_catalogued()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("lookups", problem.Detail);
    }

    [Fact]
    public async Task A_client_cannot_invent_a_product()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid lookupId = await LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets",
            new
            {
                lookupId,
                retailPrice = 0.01m,
                baseRentalPrice = 0.01m,
                minimumRentalDays = 1,
                minimumAge = 0,
                setNumber = "99999-9",
                name = "Definitely A Real Set",
                theme = "Free Stuff",
                year = 2021,
                pieceCount = 4
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        CatalogSetResponse? created = await response.Content.ReadFromJsonAsync<CatalogSetResponse>();

        Assert.NotNull(created);

        // Rebrickable's, every one of them, and the client sent something else for all four.
        Assert.Equal("10294-1", created.SetNumber);
        Assert.Equal("Titanic", created.Name);
        Assert.Equal("Icons", created.Theme);
        Assert.Equal(9092, created.PieceCount);

        // The shop's four did land, because those the client is entitled to choose.
        Assert.Equal(0.01m, created.RetailPrice);
        Assert.Equal(0, created.MinimumAge);
    }

    private static object ValidRequest(Guid lookupId, int minimumRentalDays = 7) => new
    {
        lookupId,
        retailPrice = 629.99m,
        baseRentalPrice = 60.00m,
        minimumRentalDays,
        minimumAge = 18
    };

    /// <summary>
    /// The first half of the two-call flow. Every create test needs one, because a lookupId is the
    /// only way left to name a set and only episode 27's endpoint issues one.
    /// </summary>
    private async Task<Guid> LookUpTitanicAsync(HttpClient client)
    {
        Database.Rebrickable.Sets["10294-1"] = new
        {
            set_num = "10294-1",
            name = "Titanic",
            year = 2021,
            theme_id = 252,
            num_parts = 9092,
            set_img_url = "https://cdn.rebrickable.com/media/sets/10294-1.jpg"
        };

        Database.Rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)null };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        response.EnsureSuccessStatusCode();

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        return draft.LookupId;
    }
}
