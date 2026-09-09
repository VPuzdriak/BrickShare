using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Http;

namespace BrickShare.Catalog.IntegrationTests.CatalogSets;

public class CatalogueSetTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_catalogued_set_comes_back_created()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest());

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
        Assert.Contains("setNumber", problem.Errors.Keys);
        Assert.Contains("name", problem.Errors.Keys);
        Assert.Contains("minimumRentalDays", problem.Errors.Keys);
    }

    private static object ValidRequest() => new
    {
        setNumber = "10294-1",
        name = "Titanic",
        theme = "Icons",
        year = 2021,
        pieceCount = 9090,
        retailPrice = 629.99m,
        baseRentalPrice = 60.00m,
        minimumRentalDays = 7,
        minimumAge = 18
    };
}
