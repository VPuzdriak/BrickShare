using System;
using System.Collections.Generic;
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
public class SubmitSetTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_submits_a_rebrickable_draft_and_gets_201_with_the_reference_and_theme()
    {
        Factory.SetSpecifications.Add(SetSpecificationData.Titanic());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", EntraTestTokens.Staff("Admin"),
            SetRequests.ValidBody(rebrickableSetNumber: "10294-1"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<SetResponse>(TestContext.Current.CancellationToken);
        var stored = await SetTestData.FindAsync(Factory, created!.Id);

        stored!.RebrickableSetNum.Should().Be("10294-1");
        stored.Year.Should().Be(2021);
        stored.PieceCount.Should().Be(9092);
        stored.MinifigureCount.Should().Be(3);
        stored.SpecsFetchedAt.Should().NotBeNull();

        var theme = await SetTestData.FindThemeAsync(Factory, stored.ThemeId!.Value);

        theme!.RebrickableThemeId.Should().Be(158);
        theme.Name.Should().Be("Top Theme");
    }

    [Fact]
    public async Task Submit_returns_404_problem_details_when_rebrickable_does_not_know_the_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", EntraTestTokens.Staff("Admin"),
            SetRequests.ValidBody(rebrickableSetNumber: "99999-1"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Submit_returns_503_problem_details_when_rebrickable_is_unavailable()
    {
        Factory.SetSpecifications.IsUnavailable = true;

        try
        {
            using var client = Factory.CreateClient();

            using var response = await client.SendWithTokenAsync(
                HttpMethod.Post, "/catalog/sets", EntraTestTokens.Staff("Admin"),
                SetRequests.ValidBody(rebrickableSetNumber: "10294-1"));

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        }
        finally
        {
            Factory.SetSpecifications.IsUnavailable = false;
        }
    }

    [Fact]
    public async Task Submit_returns_409_with_the_existing_set_id_when_the_reference_is_already_in_the_catalog()
    {
        Factory.SetSpecifications.Add(SetSpecificationData.Titanic());
        using var client = Factory.CreateClient();

        using var first = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", EntraTestTokens.Staff("Admin"),
            SetRequests.ValidBody(rebrickableSetNumber: "10294-1"));
        var existingId = (await first.Content.ReadFromJsonAsync<SetResponse>(TestContext.Current.CancellationToken))!.Id;

        using var second = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", EntraTestTokens.Staff("Admin"),
            SetRequests.ValidBody(rebrickableSetNumber: "10294-1"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await second.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("existingSetId").GetGuid().Should().Be(existingId);
    }

    [Fact]
    public async Task Submit_returns_400_problem_details_when_the_rebrickable_set_number_is_invalid()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/sets", EntraTestTokens.Staff("Admin"),
            SetRequests.ValidBody(rebrickableSetNumber: "10294-x"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}