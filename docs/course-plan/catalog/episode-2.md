# Episode 2 — Solution skeleton and the first endpoint

← [Course plan](catalog-api.md) · Previous: episode 1 (talk-through, no code)

The episode where the repository stops being documentation. It produces a solution, one API
project, and one endpoint that answers.

**Done when** `dotnet run` serves a request.

## What this episode is really about

Building the skeleton takes four commands. The rest of the episode is spent on what we
**don't** add, and why — because almost everything a .NET developer reaches for on day one
belongs to a later episode:

| Not here | Episode |
| --- | --- |
| Health checks | 3 |
| Test project | 4 |
| Dockerfile, Compose | 5 |
| `.editorconfig`, `Directory.Build.props`, warnings-as-errors, analyzers | 9–10 |
| A `Domain` project | 12 |
| OpenAPI / Swagger | 19 |

**One project, not four.** The Clean Architecture template's `Domain` / `Application` /
`Infrastructure` / `Api` split is structure ahead of need. A student cannot justify a layer
without an example in front of them, and four empty projects on day one teach that layering is
something you do rather than something you earn. The first project boundary in this course
appears in episode 12, when value objects with no ASP.NET dependency force it.

## The sequence

```bash
dotnet new sln -n BrickShare
dotnet new webapi -o src/Catalog/BrickShare.Catalog.Api -n BrickShare.Catalog.Api
dotnet sln add src/Catalog/BrickShare.Catalog.Api
dotnet sln migrate            # emits BrickShare.slnx
rm BrickShare.sln
dotnet new gitignore
```

Then prune the template, explaining each deletion.

### Why generate the `.sln` and then migrate it

`.slnx` could be typed by hand in thirty seconds — it is four lines. Generating the old format
first and running `dotnet sln migrate` puts both files on screen at once, and the argument makes
itself:

```
BrickShare.sln      42 lines, three GUIDs, six configuration/platform pairs
BrickShare.slnx      5 lines
```

The `.sln` for **one project** declares `Debug|x86`, `Release|x64` and four more combinations
this solution will never use, plus a `NestedProjects` section mapping folder GUIDs to project
GUIDs. None of it is wrong. All of it is why solution files conflict on every merge.

`dotnet build`, `dotnet test` and `dotnet sln` all work against `.slnx` in .NET 10. Visual
Studio 17.14+ and Rider 2025.1+ open it.

## The files

```
global.json
.gitignore
BrickShare.slnx
src/Catalog/BrickShare.Catalog.Api/
  BrickShare.Catalog.Api.csproj
  Program.cs
  appsettings.json
  appsettings.Development.json
  Properties/launchSettings.json
  BrickShare.Catalog.Api.http
```

### `Program.cs` — the whole file

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => new { service = "BrickShare Catalog API" });

app.Run();
```

Seven lines, and it proves the three things worth proving at this stage: the host starts,
routing works, and something serialises to JSON.

### `BrickShare.slnx`

```xml
<Solution>
  <Folder Name="/src/Catalog/">
    <Project Path="src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj" />
  </Folder>
</Solution>
```

`dotnet sln migrate` also emits a `<Configurations>` block listing `x64` and `x86`. It is
carried over from the `.sln` and this solution has no use for it, so it goes. That is the
episode's habit in miniature: **read what the tool gave you before accepting it.**

### `BrickShare.Catalog.Api.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

**No `ItemGroup`. Zero package references.** Worth pausing on: everything this episode builds
comes from the shared framework, and the first NuGet dependency does not arrive until
episode 15.

`Nullable` and `ImplicitUsings` stay where the template put them. Episode 9 lifts them into
`Directory.Build.props` and adds warnings-as-errors alongside — moving them is part of what
that episode is *for*, and doing it early would leave it with less to say.

### `global.json`

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

Pins the course to the SDK it was recorded on. `latestFeature` accepts newer 10.0.x feature
bands but refuses .NET 11, so a student on a newer SDK builds what you built. Five lines that
make "works on my machine" impossible from the first episode rather than the first bug report.

### `Properties/launchSettings.json`

One `http` profile on a **fixed port, 5080**. The template picks a random port per project, so
the URL on screen would not be the URL the student types.

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### `BrickShare.Catalog.Api.http`

```
@host = http://localhost:5080

### The service answers
GET {{host}}/
Accept: application/json
```

With OpenAPI stripped until episode 19, this is how the endpoint gets called — VS Code and
Rider both run `.http` files, and it costs no dependency.

### `.gitignore`

`dotnet new gitignore` writes the standard 484-line file. It matters *now*, not later: without
it, this episode's first commit carries `bin/` and `obj/` into git and every subsequent
episode's diff is buried in build output.

## What gets pruned from the template, and why

| Deleted | Why |
| --- | --- |
| The `/weatherforecast` endpoint and its `WeatherForecast` record | Sample data pretending to be a feature. It has nothing to do with a catalog service, and leaving it teaches students to build around scaffolding they never read. |
| `builder.Services.AddOpenApi()` and `app.MapOpenApi()` | OpenAPI is episode 19. Removing it is what leaves the project with zero package references. |
| `app.UseHttpsRedirection()` | This service will run in a container behind App Service, which terminates TLS. With no HTTPS port configured, the middleware redirects to a port nothing is listening on. |
| The `https` launch profile | Same reason — and it avoids sending students into the dev-certificate dance in the second episode of a course. Episode 6 shows where TLS actually gets terminated. |

`appsettings.Development.json` is kept even though its contents currently duplicate
`appsettings.json`. It is the standard place for environment overrides and starts earning its
keep in episode 15.

## Talking points

- **The template is a starting point, not a specification.** Every generated file gets read out
  loud and either justified or deleted. This is the habit that stops projects accumulating code
  nobody can explain.
- **Restraint is a design skill.** Four projects and a Swagger UI would look more impressive on
  camera and would be worse. Structure gets added when something forces it.
- **HTTPS is the platform's job**, not the container's — the first hint of where the deployment
  is going.

## Verification

```bash
dotnet build                                              # 0 warnings, 0 errors
dotnet run --project src/Catalog/BrickShare.Catalog.Api   # listening on :5080
curl http://localhost:5080/                               # {"service":"BrickShare Catalog API"}
git status --porcelain                                    # no bin/ or obj/
```

## Next

[Episode 3 — Health checks](catalog-api.md): `/health/live` and `/health/ready`, and why they
answer different questions.
