using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

public class AccessProbeTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    [Fact]
    public async Task Admin_token_is_allowed_on_admin_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/admin", EntraTestTokens.Staff("Admin"));

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Employee_token_is_forbidden_on_admin_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/admin", EntraTestTokens.Staff("Employee"));

        status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Missing_token_is_unauthorized_on_admin_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/admin");

        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_token_is_allowed_on_staff_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/staff", EntraTestTokens.Staff("Admin"));

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Employee_token_is_allowed_on_staff_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/staff", EntraTestTokens.Staff("Employee"));

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Customer_token_is_forbidden_on_staff_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/staff", EntraTestTokens.Customer());

        status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Customer_token_is_allowed_on_customer_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/customer", EntraTestTokens.Customer());

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_token_for_another_audience_is_unauthorized()
    {
        var token = EntraTestTokens.Create(
            EntraTestTokens.StaffIssuer,
            "00000000-0000-0000-0000-0000000000ff",
            new Claim("roles", "Admin"));

        var status = await GetStatusAsync("/catalog/probe/staff", token);

        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_from_unknown_issuer_is_unauthorized()
    {
        var token = EntraTestTokens.Create(
            "https://login.example.test/tenant/v2.0",
            EntraTestTokens.ClientId,
            new Claim("roles", "Admin"));

        var status = await GetStatusAsync("/catalog/probe/staff", token);

        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpStatusCode> GetStatusAsync(string path, string? token = null)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }
}