using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class OpenApiStaffSignInTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task OpenApi_document_offers_staff_sign_in_with_the_api_scope()
    {
        using var client = factory.CreateClient();
        var scope = $"api://{EntraTestTokens.ClientId}/api.access";

        var json = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(json);
        var scopes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("oauth2")
            .GetProperty("flows")
            .GetProperty("authorizationCode")
            .GetProperty("scopes");

        scopes.TryGetProperty(scope, out _).Should().BeTrue();
    }
}