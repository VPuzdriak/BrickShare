using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Api.Features.Sets;
using BrickShare.SharedKernel;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class RefreshSetSpecsTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_refreshes_the_rebrickable_facts_and_keeps_the_shop_fields()
    {
        Factory.SetSpecifications.Add(SetSpecificationData.Titanic());
        using var client = Factory.CreateClient();
        var id = await SubmitTitanicAsync(client);

        Factory.SetSpecifications.Add(
            SetSpecificationData.Titanic() with { Name = "Titanic (2nd edition)", PieceCount = 9100 });

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{id}/refresh-specs", TestTokens.Create("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await SetTestData.FindAsync(Factory, id);

        stored!.Name.Should().Be("Titanic (2nd edition)");
        stored.PieceCount.Should().Be(9100);
        stored.DailyPrice.Should().Be(new Money(1500, "EUR"));
    }

    [Fact]
    public async Task Refresh_returns_409_problem_details_for_a_set_without_a_rebrickable_reference()
    {
        using var client = Factory.CreateClient();

        using var created = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", TestTokens.Create("Admin"), SetRequests.ValidBody());
        var id = (await created.Content.ReadFromJsonAsync<SetResponse>(TestContext.Current.CancellationToken))!.Id;

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{id}/refresh-specs", TestTokens.Create("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Refresh_returns_404_problem_details_for_an_unknown_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{Guid.CreateVersion7()}/refresh-specs", TestTokens.Create("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Refresh_returns_503_problem_details_when_rebrickable_is_unavailable()
    {
        Factory.SetSpecifications.Add(SetSpecificationData.Titanic());
        using var client = Factory.CreateClient();
        var id = await SubmitTitanicAsync(client);

        Factory.SetSpecifications.IsUnavailable = true;

        try
        {
            using var response = await client.SendWithTokenAsync(
                HttpMethod.Post, $"/catalog/sets/{id}/refresh-specs", TestTokens.Create("Admin"));

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        }
        finally
        {
            Factory.SetSpecifications.IsUnavailable = false;
        }
    }

    private static async Task<Guid> SubmitTitanicAsync(HttpClient client)
    {
        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", TestTokens.Create("Admin"),
            SetRequests.ValidBody(rebrickableSetNumber: "10294-1"));

        var created = await response.Content.ReadFromJsonAsync<SetResponse>(TestContext.Current.CancellationToken);

        return created!.Id;
    }
}