# Episode 3 — Health checks

← [Course plan](catalog-api.md) · Previous: [Episode 2 — Solution skeleton](episode-2.md)

Two endpoints — `/health/live` and `/health/ready` — and the reason they are two and not one.

**Done when** both endpoints answer, and it is clear what would make each fail.

## Why two endpoints

They answer different questions, and the *response to a failure* is different in each case.
That is the whole distinction, and everything else follows from it.

| | `/health/live` | `/health/ready` |
| --- | --- | --- |
| Asks | Is this process alive? | Can this instance serve traffic? |
| Checks | **Nothing** | Every dependency |
| A failure means | The process is wedged | A dependency is unavailable |
| The right response | **Restart this instance** | **Take it out of rotation and leave it alone** |

### The failure this prevents

Wire a restart probe to a dependency check and the database going quiet for thirty seconds
restarts **every instance you own**, simultaneously. They come back cold, hit the still-struggling
database all at once, fail their probes again, and restart again.

A brief dependency blip has become a self-sustaining outage, and the platform is the thing
causing it. This is not a hypothetical — it is one of the most common ways a Kubernetes or App
Service deployment turns a small problem into a large one.

Liveness must therefore be **incapable** of failing for an external reason. The way to
guarantee that is to have it check nothing at all.

## The code

```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => new { service = "BrickShare Catalog API" });

// Liveness: is this process alive? Runs no checks at all — the only correct response to a
// failure here is to restart the instance, so it must never depend on anything external.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: can this instance serve traffic? Runs every check tagged "ready".
// Nothing is tagged yet — Postgres arrives in episode 16, Blob Storage in episode 26.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
```

**Still zero package references.** Health checks ship in the ASP.NET Core shared framework;
the `.csproj` does not change. This keeps being true until episode 16.

### `Predicate = _ => false` is the point, not a trick

It reads like a mistake — a filter that excludes every check. It is exactly right: the endpoint
runs **no** checks and returns `Healthy` as long as the process can accept a connection and
route a request. If Kestrel can answer, the process is alive. If it cannot, nothing is returned
and the probe fails on timeout, which is the signal you actually want.

### Tags are how readiness stays honest

`/health/ready` runs everything tagged `ready`. There is nothing tagged yet, so today it
returns `Healthy` having checked nothing — the same answer `/health/live` gives, for a
completely different reason.

That is not a gap to fill with a placeholder check. It is **the shape being put in place before
it is needed**, so that in episode 16 registering Postgres is one line with an obvious home:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, tags: ["ready"]);   // episode 16
```

Nothing else has to change. The endpoint, the filter and the deployment probe all already exist.

## Showing the difference on camera

Both endpoints currently return the same thing, so the wiring is invisible. Make it visible
with a deliberately broken check — added, demonstrated, and deleted before committing:

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;   // for HealthCheckResult

builder.Services.AddHealthChecks()
    .AddCheck("demo", () => HealthCheckResult.Unhealthy("pretend the database is down"),
              tags: ["ready"]);
```

```
GET /health/live     200  Healthy
GET /health/ready    503  Unhealthy
```

That is the lesson in two lines of output. Liveness is untroubled — the process is fine, and
restarting it would fix nothing. Readiness reports the truth, and the platform stops sending
this instance traffic until the dependency recovers.

**Then delete it.** It is a teaching prop, not code.

## Decisions worth stating

**The response body stays plain text.** The default writer returns `Healthy` or `Unhealthy`,
and that is enough, for two reasons. The platform probe reads the **status code** and never
looks at the body. And a detailed body on a reachable endpoint publishes your infrastructure —
which database, which dependency, what failed and why — to anyone who asks. A richer report
belongs behind authentication, if it is wanted at all.

**Health endpoints will need excluding from request logging.** A probe hitting two endpoints
every few seconds produces more log entries than real traffic does. That is dealt with in
episode 29, when logging is configured properly, and is noted here so it does not look
forgotten.

**No `/health` aggregate endpoint.** A third route that means "one of the above" invites
pointing a probe at the wrong one. Two endpoints, two purposes, no ambiguity.

## Where these get used later

| Episode | What happens |
| --- | --- |
| 6 | The App Service health probe is pointed at `/health/ready` in the portal |
| 7 | The same setting, in Terraform |
| 15 | Postgres registers a check tagged `ready` |
| 25 | Blob Storage registers one too |
| 29 | The post-deploy smoke test calls `/health/ready` before the deployment is declared good |

## Verification

```bash
dotnet build                                              # 0 warnings, 0 errors
dotnet run --project src/Catalog/BrickShare.Catalog.Api

curl -i http://localhost:5080/health/live                 # 200 Healthy
curl -i http://localhost:5080/health/ready                # 200 Healthy
```

Or run the three requests in `BrickShare.Catalog.Api.http` directly from the editor.

## Next

[Episode 4 — The first test](episode-4.md): `WebApplicationFactory`, and why the first test in
this course is an integration test rather than a unit test.
