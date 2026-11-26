using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class DeactivateSetTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_deactivates_the_set_and_gets_204()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Delete, $"/catalog/sets/{legoSet.Id}", EntraTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await SetTestData.FindAsync(Factory, legoSet.Id);
        stored!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Employee_is_forbidden_to_deactivate_a_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Delete, $"/catalog/sets/{Guid.CreateVersion7()}", EntraTestTokens.Staff("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivate_returns_404_problem_details_for_an_unknown_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Delete, $"/catalog/sets/{Guid.CreateVersion7()}", EntraTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}