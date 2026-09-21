using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Copies;

public class RegisterCopyTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_registered_copy_comes_back_with_a_label_nobody_typed()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { grade = "New", baselineWeightInGrams = 9200 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        CopyResponse? copy = await response.Content.ReadFromJsonAsync<CopyResponse>(Database.Api.Json);

        Assert.NotNull(copy);
        Assert.Equal(setId, copy.CatalogSetId);
        Assert.Equal(ConditionGrade.New, copy.Grade);
        Assert.Equal(CopyStatus.Available, copy.Status);
        Assert.Equal(9200, copy.BaselineWeightInGrams);

        // The shape, not the value. The value is the server's business and the client has no way
        // to have an opinion about it.
        Assert.Matches("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$", copy.LabelCode);
    }

    [Fact]
    public async Task The_shop_that_bought_two_boxes_calls_twice_and_gets_two_labels()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        CopyResponse first = await RegisterAsync(client, setId);
        CopyResponse second = await RegisterAsync(client, setId);

        Assert.NotEqual(first.LabelCode, second.LabelCode);
        Assert.NotEqual(first.Id, second.Id);

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(2, await dbContext.Copies.CountAsync(copy => copy.CatalogSetId == setId));
    }

    [Fact]
    public async Task A_copy_of_a_set_nobody_catalogued_is_not_a_copy_of_anything()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{Guid.CreateVersion7()}/copies",
            new { grade = "New", baselineWeightInGrams = 9200 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("catalogue", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_box_nobody_put_on_the_scale_is_refused()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { grade = "New", baselineWeightInGrams = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("baselineWeightInGrams", problem.Errors.Keys);
    }

    // Not static: it needs Database for the serializer options the API writes with.
    private async Task<CopyResponse> RegisterAsync(HttpClient client, Guid setId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { grade = "New", baselineWeightInGrams = 9200 });

        response.EnsureSuccessStatusCode();

        CopyResponse? copy = await response.Content.ReadFromJsonAsync<CopyResponse>(Database.Api.Json);

        Assert.NotNull(copy);

        return copy;
    }
}
