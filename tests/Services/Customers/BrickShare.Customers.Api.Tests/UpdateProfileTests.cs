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

public sealed class UpdateProfileTests(CustomerApiFactory factory) : IClassFixture<CustomerApiFactory>
{
    [Fact]
    public async Task Update_profile_changes_the_name_and_phone()
    {
        var customerId = Guid.CreateVersion7();
        var createdAt = factory.Time.GetUtcNow();
        var token = CustomerTestTokens.Customer(customerId, "ada@example.test", "Ada Lovelace");
        using var client = factory.CreateClient();

        var profile = await SendUpdate(client, token, new { DisplayName = "Ada King", Phone = "+44 20 7946 0000" });

        profile.Should().BeEquivalentTo(
            new CustomerProfileResponse(customerId, "ada@example.test", "Ada King", "+44 20 7946 0000", createdAt));
    }

    [Fact]
    public async Task Update_profile_rejects_a_blank_display_name()
    {
        var token = CustomerTestTokens.Customer(Guid.CreateVersion7(), "ada@example.test", "Ada Lovelace");
        using var client = factory.CreateClient();

        using var response = await Send(client, token, new { DisplayName = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_profile_is_forbidden_for_a_staff_token()
    {
        using var client = factory.CreateClient();

        using var response = await Send(client, CustomerTestTokens.Staff("Admin"), new { DisplayName = "Ada King" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<CustomerProfileResponse> SendUpdate(HttpClient client, string token, object body)
    {
        using var response = await Send(client, token, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<CustomerProfileResponse>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/customers/me")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}