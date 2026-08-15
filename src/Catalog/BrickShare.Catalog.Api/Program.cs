using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

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
