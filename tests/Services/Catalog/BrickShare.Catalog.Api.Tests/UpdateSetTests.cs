using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Api.Features.Sets;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class UpdateSetTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_updates_the_set_and_gets_200()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Put,
            $"/catalog/sets/{legoSet.Id}",
            EntraTestTokens.Staff("Admin"),
            SetRequests.ValidBody(legoSetNumber: "75192", name: "Millennium Falcon UCS"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SetResponse>(TestContext.Current.CancellationToken);
        body!.LegoSetNumber.Should().Be("75192");
        body.Name.Should().Be("Millennium Falcon UCS");

        var stored = await SetTestData.FindAsync(Factory, legoSet.Id);
        stored!.Name.Should().Be("Millennium Falcon UCS");
    }

    [Fact]
    public async Task Employee_is_forbidden_to_update_a_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Put,
            $"/catalog/sets/{Guid.CreateVersion7()}",
            EntraTestTokens.Staff("Employee"),
            SetRequests.ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_returns_400_problem_details_when_name_is_empty()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Put,
            $"/catalog/sets/{legoSet.Id}",
            EntraTestTokens.Staff("Admin"),
            SetRequests.ValidBody(name: ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").TryGetProperty("name", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Update_returns_404_problem_details_for_an_unknown_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Put,
            $"/catalog/sets/{Guid.CreateVersion7()}",
            EntraTestTokens.Staff("Admin"),
            SetRequests.ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}