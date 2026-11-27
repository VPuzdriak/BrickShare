using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Domain;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class LookupInstanceByCodeTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Employee_looks_up_a_scanned_code_and_gets_the_instance_set_and_status()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        var instance = await SetTestData.SaveInstanceAsync(Factory, SetTestData.CreateInstance(legoSet.Id));
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, $"/catalog/instances/by-code/{instance.InventoryCode.ToString()}", EntraTestTokens.Staff("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var scanned = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        scanned.GetProperty("id").GetGuid().Should().Be(instance.Id);
        scanned.GetProperty("inventoryCode").GetString().Should().Be(instance.InventoryCode.ToString());
        scanned.GetProperty("setId").GetGuid().Should().Be(legoSet.Id);
        scanned.GetProperty("setName").GetString().Should().Be("Millennium Falcon");
        scanned.GetProperty("legoSetNumber").GetString().Should().Be("10294");
        scanned.GetProperty("condition").GetString().Should().Be(InstanceCondition.New);
        scanned.GetProperty("status").GetString().Should().Be(InstanceStatus.Available);
    }

    [Fact]
    public async Task Lookup_accepts_a_code_typed_in_lowercase_without_dashes()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        var instance = await SetTestData.SaveInstanceAsync(Factory, SetTestData.CreateInstance(legoSet.Id));
        var typed = instance.InventoryCode.ToString().Replace("-", string.Empty).ToLowerInvariant();
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, $"/catalog/instances/by-code/{typed}", EntraTestTokens.Staff("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lookup_returns_400_problem_details_for_a_code_with_a_wrong_check_character()
    {
        var valid = InventoryCode.FromSequence(1).Value;
        var wrongCheck = valid[..^1] + (valid[^1] == 'A' ? 'E' : 'A');
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, $"/catalog/instances/by-code/{wrongCheck}", EntraTestTokens.Staff("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Lookup_returns_404_problem_details_for_a_valid_code_no_instance_has()
    {
        var unused = InventoryCode.FromSequence(2).ToString();
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, $"/catalog/instances/by-code/{unused}", EntraTestTokens.Staff("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Customer_is_forbidden_to_look_up_a_code()
    {
        var code = InventoryCode.FromSequence(1).ToString();
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Get, $"/catalog/instances/by-code/{code}", EntraTestTokens.Customer());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}