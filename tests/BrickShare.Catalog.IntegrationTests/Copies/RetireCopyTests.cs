using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Copies;

public class RetireCopyTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_retired_copy_is_still_a_row()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid copyId = await RegisterOneCopyAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/catalog/copies/{copyId}/retirement", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // A second connection, because the question is what the database holds and not what some
        // change tracker remembers being told.
        await using CatalogDbContext dbContext = Database.NewDbContext();
        Copy? retired = await dbContext.Copies.SingleOrDefaultAsync(copy => copy.Id == copyId);

        // The three assertions the episode is named after.
        Assert.NotNull(retired);
        Assert.Equal(CopyStatus.Retired, retired.Status);
        Assert.NotNull(retired.RetiredAt);
        Assert.StartsWith("BRK-", retired.Label.Value);
    }

    [Fact]
    public async Task A_copy_nobody_registered_cannot_be_retired()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/catalog/copies/{Guid.CreateVersion7()}/retirement", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("not registered", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_copy_that_is_out_on_rent_cannot_be_retired()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid copyId = await RegisterOneCopyAsync(client);
        await SendOnRentAsync(copyId);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/catalog/copies/{copyId}/retirement", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);

        // The domain wrote this sentence, in episode 15, for a person to read.
        Assert.Contains("OnRent", problem.Detail);
        Assert.True(problem.Extensions.ContainsKey("traceId"));

        // And the refusal is a refusal: nothing was written.
        await using CatalogDbContext dbContext = Database.NewDbContext();
        Copy? unchanged = await dbContext.Copies.SingleOrDefaultAsync(copy => copy.Id == copyId);

        Assert.NotNull(unchanged);
        Assert.Equal(CopyStatus.OnRent, unchanged.Status);
        Assert.Null(unchanged.RetiredAt);
    }

    [Fact]
    public async Task A_copy_cannot_be_retired_twice()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid copyId = await RegisterOneCopyAsync(client);

        HttpResponseMessage first = await client.PostAsync(
            $"/api/v1/catalog/copies/{copyId}/retirement", content: null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        DateTimeOffset? retiredAt;

        await using (CatalogDbContext afterFirst = Database.NewDbContext())
        {
            retiredAt = (await afterFirst.Copies
                .SingleAsync(copy => copy.Id == copyId)).RetiredAt;
        }

        HttpResponseMessage second = await client.PostAsync(
            $"/api/v1/catalog/copies/{copyId}/retirement", content: null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // The second call did not move the timestamp. A retirement has a date, and that date is
        // the day it actually happened.
        await using CatalogDbContext afterSecond = Database.NewDbContext();
        Copy still = await afterSecond.Copies.SingleAsync(copy => copy.Id == copyId);

        Assert.Equal(retiredAt, still.RetiredAt);
    }


    /// <summary>
    /// One box on the shelf, through the front door. The id comes back in the registration
    /// response because there is still no GET for a copy — episode 34.
    /// </summary>
    private async Task<Guid> RegisterOneCopyAsync(HttpClient client)
    {
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { new { grade = "New", baselineWeightInGrams = 9200 } } });

        response.EnsureSuccessStatusCode();

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);

        return Assert.Single(registered.Copies).Id;
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
