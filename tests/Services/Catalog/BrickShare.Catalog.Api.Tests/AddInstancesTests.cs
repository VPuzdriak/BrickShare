using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Api.Features.Instances;
using BrickShare.Catalog.Domain;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class AddInstancesTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_adds_instances_and_gets_201_with_their_codes()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        var added = await AddAsync(client, legoSet.Id, quantity: 3);

        added.Should().HaveCount(3);
        added.Should().OnlyContain(instance => instance.Status == InstanceStatus.Available);
        added.Should().OnlyContain(instance => instance.Condition == InstanceCondition.New);
        added.Should().OnlyContain(instance => InventoryCode.Parse(instance.InventoryCode).IsSuccess);
    }

    [Fact]
    public async Task Codes_stay_unique_across_requests()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        var first = await AddAsync(client, legoSet.Id, quantity: 2);
        var second = await AddAsync(client, legoSet.Id, quantity: 2);

        first.Concat(second).Select(instance => instance.InventoryCode).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Add_returns_400_problem_details_when_quantity_is_below_one()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{legoSet.Id}/instances", TestTokens.Create("Admin"),
            new { quantity = 0, condition = InstanceCondition.New });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Add_returns_404_problem_details_for_an_unknown_set()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{Guid.CreateVersion7()}/instances", TestTokens.Create("Admin"),
            new { quantity = 1, condition = InstanceCondition.New });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Employee_is_forbidden_to_add_instances()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{legoSet.Id}/instances", TestTokens.Create("Employee"),
            new { quantity = 1, condition = InstanceCondition.New });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<List<InstanceResponse>> AddAsync(HttpClient client, Guid setId, int quantity)
    {
        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{setId}/instances", TestTokens.Create("Admin"),
            new { quantity, condition = InstanceCondition.New });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<List<InstanceResponse>>(
            TestContext.Current.CancellationToken) ?? [];
    }
}