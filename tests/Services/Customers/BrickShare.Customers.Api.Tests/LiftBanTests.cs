using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Customers.Domain;

using Xunit;

namespace BrickShare.Customers.Api.Tests;

public sealed class LiftBanTests(CustomerApiFactory factory) : IClassFixture<CustomerApiFactory>
{
    [Fact]
    public async Task Admin_lifts_a_ban_and_gets_204()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        var banId = await CustomerTestData.BanAsync(factory, customerId);
        using var client = factory.CreateClient();

        using var response = await Send(client, customerId, banId, CustomerTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await CustomerTestData.FindAsync(factory, customerId);
        stored.Status.Should().Be(CustomerStatus.Active);
        stored.Bans.Should().ContainSingle().Which.LiftedAt.Should().Be(factory.Time.GetUtcNow());
    }

    [Fact]
    public async Task Lift_returns_404_problem_details_for_an_unknown_ban()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        using var client = factory.CreateClient();

        using var response = await Send(client, customerId, Guid.CreateVersion7(), CustomerTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Lift_returns_409_problem_details_when_the_ban_is_already_lifted()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        var banId = await CustomerTestData.BanAsync(factory, customerId);
        var token = CustomerTestTokens.Staff("Admin");
        using var client = factory.CreateClient();
        using var first = await Send(client, customerId, banId, token);
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var second = await Send(client, customerId, banId, token);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Lift_is_forbidden_for_an_employee()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        var banId = await CustomerTestData.BanAsync(factory, customerId);
        using var client = factory.CreateClient();

        using var response = await Send(client, customerId, banId, CustomerTestTokens.Staff("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, Guid customerId, Guid banId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/customers/{customerId}/bans/{banId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}