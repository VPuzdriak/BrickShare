using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

public class AccessProbeTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    [Fact]
    public async Task Admin_token_is_allowed_on_admin_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/admin", TestTokens.Create("Admin"));

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Employee_token_is_forbidden_on_admin_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/admin", TestTokens.Create("Employee"));

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
        var status = await GetStatusAsync("/catalog/probe/staff", TestTokens.Create("Admin"));

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Employee_token_is_allowed_on_staff_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/staff", TestTokens.Create("Employee"));

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Customer_token_is_forbidden_on_staff_probe()
    {
        var status = await GetStatusAsync("/catalog/probe/staff", TestTokens.Create("Customer"));

        status.Should().Be(HttpStatusCode.Forbidden);
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