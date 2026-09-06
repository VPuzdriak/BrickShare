using Azure.Core;
using Azure.Identity;

using BrickShare.Catalog.Api.Persistence;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Catalog"));
    /*
     A connection string that carries a password is Compose or Testcontainers, and it is left
     exactly as it is. One with no password is Azure, where the password is a token that has to
     be fetched and expires.
    */
    if (string.IsNullOrEmpty(dataSourceBuilder.ConnectionStringBuilder.Password))
    {
        var credential = new DefaultAzureCredential();

        dataSourceBuilder.UsePasswordProvider(
            passwordProvider: _ =>
                throw new NotSupportedException(
                    "Open connections asynchronously: fetching a token from a blocking Open() deadlocks."),
            passwordProviderAsync: async (_, cancellationToken) =>
            {
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]),
                    cancellationToken);

                return token.Token;
            }
        );
    }

    return dataSourceBuilder.Build();
});

builder.Services.AddDbContext<CatalogDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>(tags: ["ready"]);

var app = builder.Build();

app.MapGet("/", () => new { service = "BrickShare Catalog API" });

// Liveness: is this process alive? Runs no checks at all — the only correct response to a
// failure here is to restart the instance, so it must never depend on anything external.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: can this instance serve traffic? Runs every check tagged "ready".
// Nothing is tagged yet — Postgres arrives in episode 15, Blob Storage in episode 25.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();

// Exposes the generated entry point so WebApplicationFactory<Program> can find it in tests.
#pragma warning disable S1118
// S1118 wants a private constructor on a class with no instance members. This class cannot
// have one: WebApplicationFactory<Program> needs a public, constructible entry point type.
// The rule is right in general and wrong here, so it is turned off for these two lines only.
namespace BrickShare.Catalog.Api
{
    public partial class Program;
}
#pragma warning restore S1118
