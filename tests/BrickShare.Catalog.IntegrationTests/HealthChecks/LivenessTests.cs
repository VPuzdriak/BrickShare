using System.Net;

namespace BrickShare.Catalog.IntegrationTests.HealthChecks;

public class LivenessTests
{
    [Fact]
    public async Task Live_returns_ok()
    {
        await using CatalogApiFactory api = new(connectionString: null);

        HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
