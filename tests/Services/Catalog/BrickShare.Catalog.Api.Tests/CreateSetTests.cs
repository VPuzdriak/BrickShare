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
public class CreateSetTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_creates_a_set_and_gets_201_with_its_location()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", TestTokens.Create("Admin"), SetRequests.ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().StartWith("/catalog/sets/");

        var created = await response.Content.ReadFromJsonAsync<SetResponse>(TestContext.Current.CancellationToken);
        created!.Name.Should().Be("Millennium Falcon");
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Employee_is_forbidden_to_create_a_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", TestTokens.Create("Employee"), SetRequests.ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_returns_400_problem_details_when_name_is_empty()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", TestTokens.Create("Admin"), SetRequests.ValidBody(name: ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").TryGetProperty("name", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_returns_400_problem_details_when_the_set_number_has_a_suffix()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", TestTokens.Create("Admin"), SetRequests.ValidBody(legoSetNumber: "10294-1"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").TryGetProperty("legoSetNumber", out _).Should().BeTrue();
    }
}