using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Azure.Core;
using Azure.Identity;

using BrickShare.Catalog.Api;
using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using FluentValidation;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

/*
 Azure only, and driven by whether the setting exists rather than by which environment this is.
 With no KeyVault:Uri the provider is never added, so a laptop keeps reading `dotnet user-secrets`
 and Compose keeps reading the environment — episode 25, step 6, unchanged.
*/
if (builder.Configuration["KeyVault:Uri"] is { Length: > 0 } keyVaultUri)
{
    // Added last, so it wins. Nothing in appsettings.json sets Rebrickable:ApiKey today, and this
    // ordering is what keeps the answer obvious if anything ever does.
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

// FluentValidation names errors after C# properties. The wire is camelCase, so the two have to be
// reconciled somewhere, and this is the only place FluentValidation offers.
ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
    member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);

builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);

// Grades and statuses cross the wire as "New" and "Available", never as 0 and 1. An ordinal is a
// position in a C# declaration: insert a grade between Excellent and Good and every client in the
// world silently changes its mind about what it is asking for. The database already made this
// call — CopyConfiguration stores both columns as strings — and the wire now agrees with it.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<ILabelCodeMinter, RandomLabelCodeMinter>();

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

builder.Services.AddRebrickable(builder.Configuration);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "BrickShare Catalog API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Staff-facing catalog service: look a set up, catalogue it, register copies of it, retire a copy.";

        return Task.CompletedTask;
    });
});

builder.Services.AddExceptionHandler<DomainRuleViolationExceptionHandler>();

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

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/", () => new { service = "BrickShare Catalog API" })
    .ExcludeFromDescription();

// Liveness: is this process alive? Runs no checks at all — the only correct response to a
// failure here is to restart the instance, so it must never depend on anything external.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: can this instance serve traffic? Runs every check tagged "ready".
// Nothing is tagged yet — Postgres arrives in episode 15, Blob Storage in episode 25.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Everything a client calls lives under a version. See below: this is a prefix, not a library.
RouteGroupBuilder v1 = app.MapGroup("/api/v1");

v1.MapCatalogSets();
v1.MapCatalogLookups();
v1.MapCopies();
v1.MapCopyRetirements();

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
