using System.Diagnostics;
using System.Text.Json;

using Azure.Core;
using Azure.Identity;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;

using FluentValidation;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// FluentValidation names errors after C# properties. The wire is camelCase, so the two have to be
// reconciled somewhere, and this is the only place FluentValidation offers.
ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
    member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);

builder.Services.AddScoped<IValidator<CatalogueSetRequest>, CatalogueSetRequestValidator>();

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
                    "Open connections asynchronously: a blocking Open() would hold a thread-pool thread for the length of a network call."),
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

// Turns any unhandled failure into RFC 9457 instead of an empty body.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;

        // One id that appears in the response and in the logs, so a screenshot from a staff
        // member is enough to find the request. Episode 34 wires the other end of this.
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    });

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>(tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/", () => new { service = "BrickShare Catalog API" });

// Liveness: is this process alive? Runs no checks at all — the only correct response to a
// failure here is to restart the instance, so it must never depend on anything external.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: can this instance serve traffic? Runs every check tagged "ready".
// Nothing is tagged yet — Postgres arrives in episode 15, Blob Storage in episode 25.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Everything a client calls lives under a version. See below: this is a prefix, not a library.
RouteGroupBuilder v1 = app.MapGroup("/api/v1");

v1.MapCatalogSets();

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
