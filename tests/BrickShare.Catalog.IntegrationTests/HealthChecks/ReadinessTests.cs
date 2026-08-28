using System.Net;

namespace BrickShare.Catalog.IntegrationTests.HealthChecks;

public class ReadinessTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task Ready_reports_healthy_when_the_database_answers()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
