using System.Net;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

public class HealthCheckTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    [Fact]
    public async Task Liveness_check_returns_ok()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}