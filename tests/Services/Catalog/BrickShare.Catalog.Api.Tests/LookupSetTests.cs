using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class LookupSetTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_gets_the_set_draft_for_a_set_number_without_a_variant()
    {
        Factory.SetSpecifications.Add(SetSpecificationData.Titanic());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, "/catalog/sets/lookup/10294", EntraTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var draft = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        draft.GetProperty("legoSetNumber").GetString().Should().Be("10294");
        draft.GetProperty("rebrickableSetNumber").GetString().Should().Be("10294-1");
        draft.GetProperty("name").GetString().Should().Be("Titanic");
        draft.GetProperty("pieceCount").GetInt32().Should().Be(9092);
        draft.GetProperty("minifigureCount").GetInt32().Should().Be(3);
        draft.GetProperty("rebrickableThemeId").GetInt32().Should().Be(158);
        draft.GetProperty("themeName").GetString().Should().Be("Top Theme");
    }

    [Fact]
    public async Task Lookup_returns_404_problem_details_when_rebrickable_does_not_know_the_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, "/catalog/sets/lookup/99999", EntraTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Lookup_returns_400_problem_details_for_an_invalid_set_number()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, "/catalog/sets/lookup/abc", EntraTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Employee_is_forbidden_to_look_up_a_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, "/catalog/sets/lookup/10294", EntraTestTokens.Staff("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Lookup_returns_503_problem_details_when_rebrickable_is_unavailable()
    {
        Factory.SetSpecifications.IsUnavailable = true;

        try
        {
            using var client = Factory.CreateClient();

            using var response = await client.SendWithTokenAsync(
                HttpMethod.Get, "/catalog/sets/lookup/10294", EntraTestTokens.Staff("Admin"));

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        }
        finally
        {
            Factory.SetSpecifications.IsUnavailable = false;
        }
    }
}