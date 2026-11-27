using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Customers.Domain;

using Xunit;

namespace BrickShare.Customers.Api.Tests;

public sealed class BanCustomerTests(CustomerApiFactory factory) : IClassFixture<CustomerApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Admin_bans_a_customer_and_gets_201_with_the_ban()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        var adminId = Guid.CreateVersion7();
        using var client = factory.CreateClient();

        using var response = await Send(
            client,
            customerId,
            CustomerTestTokens.Staff(adminId, "Admin"),
            new { Reason = BanReason.LateReturn, Comment = "Returned three days late." });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().StartWith($"/customers/{customerId}/bans/");

        var ban = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        ban.GetProperty("reason").GetString().Should().Be("LateReturn");
        ban.GetProperty("comment").GetString().Should().Be("Returned three days late.");
        ban.GetProperty("bannedBy").GetString().Should().Be(adminId.ToString());
    }

    [Fact]
    public async Task Ban_stores_the_ban_and_sets_the_status_to_banned()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        using var client = factory.CreateClient();

        using var response = await Send(
            client,
            customerId,
            CustomerTestTokens.Staff("Admin"),
            new { Reason = BanReason.NonPayment, Comment = "Payment was not received." });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var stored = await CustomerTestData.FindAsync(factory, customerId);
        stored.Status.Should().Be(CustomerStatus.Banned);

        var ban = stored.Bans.Should().ContainSingle().Which;
        ban.Reason.Should().Be(BanReason.NonPayment);
        ban.Comment.Should().Be("Payment was not received.");
        ban.From.Should().Be(factory.Time.GetUtcNow());
        ban.LiftedAt.Should().BeNull();
    }

    [Fact]
    public async Task Ban_returns_400_problem_details_for_a_blank_comment()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        using var client = factory.CreateClient();

        using var response = await Send(
            client,
            customerId,
            CustomerTestTokens.Staff("Admin"),
            new { Reason = BanReason.Other, Comment = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Ban_returns_404_problem_details_for_an_unknown_customer()
    {
        using var client = factory.CreateClient();

        using var response = await Send(
            client,
            Guid.CreateVersion7(),
            CustomerTestTokens.Staff("Admin"),
            new { Reason = BanReason.Other, Comment = "Staff note." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Ban_is_forbidden_for_an_employee()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        using var client = factory.CreateClient();

        using var response = await Send(
            client,
            customerId,
            CustomerTestTokens.Staff("Employee"),
            new { Reason = BanReason.Other, Comment = "Staff note." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ban_is_forbidden_for_a_customer_token()
    {
        var customerId = await CustomerTestData.SaveCustomerAsync(factory);
        using var client = factory.CreateClient();
        var token = CustomerTestTokens.Customer(customerId, "ada@example.test", "Ada Lovelace");

        using var response = await Send(
            client,
            customerId,
            token,
            new { Reason = BanReason.Other, Comment = "Staff note." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, Guid customerId, string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/customers/{customerId}/bans")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}