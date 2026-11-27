using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Customers.Api.Features.Customers.GetProfile;

using Xunit;

namespace BrickShare.Customers.Api.Tests;

public sealed class GetProfileTests(CustomerApiFactory factory) : IClassFixture<CustomerApiFactory>
{
    [Fact]
    public async Task Get_profile_creates_the_customer_on_the_first_call()
    {
        var customerId = Guid.CreateVersion7();
        var createdAt = factory.Time.GetUtcNow();
        using var client = factory.CreateClient();
        var token = CustomerTestTokens.Customer(customerId, "ada@example.test", "Ada Lovelace");

        var profile = await ReadProfile(client, token);

        profile.Should().BeEquivalentTo(
            new CustomerProfileResponse(customerId, "ada@example.test", "Ada Lovelace", null, createdAt));
    }

    [Fact]
    public async Task Get_profile_returns_the_same_customer_on_later_calls()
    {
        var customerId = Guid.CreateVersion7();
        var token = CustomerTestTokens.Customer(customerId, "ada@example.test", "Ada Lovelace");
        using var client = factory.CreateClient();
        var first = await ReadProfile(client, token);

        factory.Time.Advance(TimeSpan.FromHours(1));
        var second = await ReadProfile(client, token);

        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task Get_profile_needs_a_token()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/customers/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_profile_is_forbidden_for_a_staff_token()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/customers/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CustomerTestTokens.Staff("Admin"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<CustomerProfileResponse> ReadProfile(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/customers/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<CustomerProfileResponse>(TestContext.Current.CancellationToken))!;
    }
}