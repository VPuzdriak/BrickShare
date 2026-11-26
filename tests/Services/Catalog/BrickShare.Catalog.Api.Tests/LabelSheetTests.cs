using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class LabelSheetTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_gets_one_sheet_with_a_label_for_each_instance()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        var first = await SetTestData.SaveInstanceAsync(Factory, SetTestData.CreateInstance(legoSet.Id, sequence: 1));
        var second = await SetTestData.SaveInstanceAsync(Factory, SetTestData.CreateInstance(legoSet.Id, sequence: 2));
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/instances/labels", TestTokens.Create("Admin"),
            new { instanceIds = new[] { first.Id, second.Id } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");

        var sheet = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        sheet.Should().Contain(first.InventoryCode.ToString());
        sheet.Should().Contain(second.InventoryCode.ToString());
    }

    [Fact]
    public async Task Sheet_returns_404_problem_details_when_an_instance_is_unknown()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/instances/labels", TestTokens.Create("Admin"),
            new { instanceIds = new[] { Guid.CreateVersion7() } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Sheet_returns_400_problem_details_when_no_instance_is_given()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, "/catalog/instances/labels", TestTokens.Create("Admin"),
            new { instanceIds = Array.Empty<Guid>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}