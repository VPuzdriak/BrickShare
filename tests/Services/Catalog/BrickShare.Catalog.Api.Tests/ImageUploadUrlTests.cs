using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class ImageUploadUrlTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Admin_gets_an_upload_url_for_a_new_image_of_the_set()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{legoSet.Id}/images/upload-url", EntraTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var imageId = body.GetProperty("imageId").GetGuid();
        body.GetProperty("uploadUrl").GetString().Should().StartWith("https://storage.test/set-images/");
        Factory.SetImageStorage.LastBlobName.Should().Be($"originals/{legoSet.Id}/{imageId}");
    }

    [Fact]
    public async Task Upload_url_returns_404_problem_details_for_an_unknown_set()
    {
        var unknownSetId = Guid.CreateVersion7();
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{unknownSetId}/images/upload-url", EntraTestTokens.Staff("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        Factory.SetImageStorage.LastBlobName.Should().NotContain(unknownSetId.ToString());
    }

    [Fact]
    public async Task Employee_is_forbidden_to_request_an_upload_url()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{legoSet.Id}/images/upload-url", EntraTestTokens.Staff("Employee"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_url_expires_fifteen_minutes_after_it_is_issued()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        using var client = Factory.CreateClient();

        using var response = await client.SendWithTokenAsync(
            HttpMethod.Post, $"/catalog/sets/{legoSet.Id}/images/upload-url", EntraTestTokens.Staff("Admin"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("expiresAt").GetDateTimeOffset().Should().Be(Factory.Time.GetUtcNow().AddMinutes(15));
    }
}