using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Catalog.IntegrationTests.Copies;

public class RegisterCopiesTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task One_box_is_a_delivery_of_one()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { copies = new[] { Weighing(9200) } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);

        CopyResponse copy = Assert.Single(registered.Copies);

        Assert.Equal(setId, copy.CatalogSetId);
        Assert.Equal(ConditionGrade.New, copy.Grade);
        Assert.Equal(CopyStatus.Available, copy.Status);
        Assert.Equal(9200, copy.BaselineWeightInGrams);

        // The shape, not the value. The value is the server's business.
        Assert.Matches("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$", copy.LabelCode);
    }

    [Fact]
    public async Task Seventeen_boxes_arrive_in_one_call_and_get_seventeen_labels()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        // Seventeen different weights, because seventeen boxes are seventeen objects.
        object[] delivery = [.. Enumerable.Range(0, 17).Select(index => Weighing(9200 + index))];

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { copies = delivery });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);
        Assert.Equal(17, registered.Copies.Count);
        Assert.Equal(17, registered.Copies.Select(copy => copy.LabelCode).Distinct().Count());

        // The order matters: the third label in the response belongs to the third box in the
        // delivery, and that is how staff know which sticker goes on which box.
        Assert.Equal(
            Enumerable.Range(0, 17).Select(index => 9200 + index),
            registered.Copies.Select(copy => copy.BaselineWeightInGrams));

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(17, await dbContext.Copies.CountAsync(copy => copy.CatalogSetId == setId));
    }

    [Fact]
    public async Task One_bad_weight_registers_none_of_them()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { Weighing(9200), Weighing(9187), Weighing(0), Weighing(9210) } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);

        // The index is the point. "One of your four weights is wrong" is not a usable answer.
        Assert.Contains("copies[2].baselineWeightInGrams", problem.Errors.Keys);

        // And the reason this test exists at all.
        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(0, await dbContext.Copies.CountAsync());
    }

    [Fact]
    public async Task An_empty_delivery_is_not_a_delivery()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { copies = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("copies", problem.Errors.Keys);
    }

    [Fact]
    public async Task A_copy_of_a_set_nobody_catalogued_is_not_a_copy_of_anything()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{Guid.CreateVersion7()}/copies",
            new { copies = new[] { Weighing(9200) } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("catalogue", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_batch_that_mints_the_same_label_twice_mints_the_whole_batch_again()
    {
        // Attempt one collides with itself; attempt two is two labels nobody holds.
        ScriptedLabelCodeMinter minter = new(
            "BRK-AAAAAA", "BRK-AAAAAA",
            "BRK-BBBBBB", "BRK-CCCCCC");

        await using WebApplicationFactory<Program> api = Database.Api.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<ILabelCodeMinter>(minter)));

        HttpClient client = api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { Weighing(9200), Weighing(9187) } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);

        // Both labels are from the second attempt. Not one kept and one re-minted — a box was
        // never going to be dropped, and this is the assertion that says so.
        Assert.Equal(["BRK-BBBBBB", "BRK-CCCCCC"], registered.Copies.Select(copy => copy.LabelCode));

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(2, await dbContext.Copies.CountAsync());
    }


    private static object Weighing(int grams) =>
        new { grade = "New", baselineWeightInGrams = grams };
}
