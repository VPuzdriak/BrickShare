using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BrickShare.Catalog.IntegrationTests;

/// <summary>
/// The API, wired to the test container. It overrides one configuration key and nothing else:
/// every service registration in Program.cs is the one that runs in production.
/// </summary>
public sealed class CatalogApiFactory(string? connectionString, string rebrickableBaseAddress)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = connectionString,
                ["Rebrickable:BaseAddress"] = rebrickableBaseAddress,
                ["Rebrickable:ApiKey"] = "test-key",
                ["Rebrickable:Resilience:Retry:Delay"] = "00:00:00.001"
            }));

    /// <summary>
    /// The options this API serialises with, read back out of the running host rather than restated
    /// here. A test is a client, and a client that guesses the wire format is a test that can pass
    /// for the wrong reason.
    /// </summary>
    public JsonSerializerOptions Json =>
        Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
}
