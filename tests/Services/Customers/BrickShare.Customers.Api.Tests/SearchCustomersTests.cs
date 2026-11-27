using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Customers.Api.Features.Customers.SearchCustomers;

using Xunit;

namespace BrickShare.Customers.Api.Tests;

public sealed class SearchCustomersTests(CustomerApiFactory factory) : IClassFixture<CustomerApiFactory>
{
    [Fact]
    public async Task Search_finds_customers_by_part_of_the_name_ignoring_case()
    {
        using var client = factory.CreateClient();
        await CreateCustomer(client, "Quill Harbor", "quill@example.test");
        await CreateCustomer(client, "Grace Hopper", "grace@example.test");

        var results = await Search(client, "HARBOR");

        results.Should().ContainSingle().Which.DisplayName.Should().Be("Quill Harbor");
    }

    [Fact]
    public async Task Search_treats_percent_signs_as_plain_text()
    {
        using var client = factory.CreateClient();
        await CreateCustomer(client, "100% Brick", "percent@example.test");
        await CreateCustomer(client, "1000 Brick", "thousand@example.test");

        var results = await Search(client, "100%");

        results.Should().ContainSingle().Which.DisplayName.Should().Be("100% Brick");
    }

    [Fact]
    public async Task Search_needs_search_text()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/customers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CustomerTestTokens.Staff("Employee"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_is_forbidden_for_a_customer_token()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/customers?search=quill");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", CustomerTestTokens.Customer(Guid.CreateVersion7(), "someone@example.test", "Someone"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task CreateCustomer(HttpClient client, string name, string email)
    {
        var token = CustomerTestTokens.Customer(Guid.CreateVersion7(), email, name);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/customers/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<List<CustomerSummaryResponse>> Search(HttpClient client, string term)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/customers?search={Uri.EscapeDataString(term)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CustomerTestTokens.Staff("Employee"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<List<CustomerSummaryResponse>>(TestContext.Current.CancellationToken))!;
    }
}