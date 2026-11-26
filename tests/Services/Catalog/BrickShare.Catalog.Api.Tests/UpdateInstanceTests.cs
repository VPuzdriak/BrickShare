using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Domain;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class UpdateInstanceTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_changes_the_condition_and_status_and_gets_200()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        var instance = await SetTestData.SaveInstanceAsync(Factory, SetTestData.CreateInstance(legoSet.Id));
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Put, $"/catalog/instances/{instance.Id}", TestTokens.Create("Admin"),
            new { condition = InstanceCondition.Worn, status = InstanceStatus.InMaintenance });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await SetTestData.FindInstanceAsync(Factory, instance.Id);
        stored!.Condition.Should().Be(InstanceCondition.Worn);
        stored.Status.Should().Be(InstanceStatus.InMaintenance);
    }

    [Fact]
    public async Task Update_returns_404_problem_details_for_an_unknown_instance()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Put, $"/catalog/instances/{Guid.CreateVersion7()}", TestTokens.Create("Admin"),
            new { condition = InstanceCondition.Good, status = InstanceStatus.Available });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Employee_is_forbidden_to_change_instances()
    {
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Put, $"/catalog/instances/{Guid.CreateVersion7()}", TestTokens.Create("Employee"),
            new { condition = InstanceCondition.Good, status = InstanceStatus.Available });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}