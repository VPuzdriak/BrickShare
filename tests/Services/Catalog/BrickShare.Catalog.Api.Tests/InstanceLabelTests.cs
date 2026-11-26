using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class InstanceLabelTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_gets_an_svg_label_with_the_code_set_name_and_set_number()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        var instance = await SetTestData.SaveInstanceAsync(Factory, SetTestData.CreateInstance(legoSet.Id));
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, $"/catalog/instances/{instance.Id}/label", TestTokens.Create("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");

        var label = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        label.Should().Contain(instance.InventoryCode.ToString());
        label.Should().Contain("Millennium");
        label.Should().Contain("10294");
        label.Should().NotContain(instance.Id.ToString());
    }

    [Fact]
    public async Task Label_returns_404_problem_details_for_an_unknown_instance()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, $"/catalog/instances/{Guid.CreateVersion7()}/label", TestTokens.Create("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Employee_is_forbidden_to_get_a_label()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        var instance = await SetTestData.SaveInstanceAsync(Factory, SetTestData.CreateInstance(legoSet.Id));
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, $"/catalog/instances/{instance.Id}/label", TestTokens.Create("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}