# Episode 4 — The first test

← [Course plan](catalog-api.md) · Previous: [Episode 3 — Health checks](episode-3.md)

The test project, and one integration test asserting `/health/live` returns 200.

**Done when** `dotnet test` is green.

## Say this out loud: this is not TDD

There is no behaviour to drive out yet. The test protects that the application **starts and is
wired correctly** — a different job, and one worth doing on its own terms. Calling this TDD
would claim a practice for ten episodes that do not use it, and spend credibility that episode
11 needs when TDD actually starts, on a real rule, with red-green-refactor shown properly.

## Why an integration test, and not a unit test

At this point in the course there is nothing to unit test. `Program.cs` has no logic in it —
no branch, no calculation, nothing a unit test could isolate. The only thing that **can** break
is **composition**: a service not registered, a middleware in the wrong order, configuration
that doesn't bind, the host failing to start at all.

A unit test cannot see any of that, because a unit test never builds the host. Only something
that boots the real `WebApplicationFactory` pipeline can catch a DI registration that's missing
— which is exactly the class of bug this episode's own code could have introduced.

**This gets proven on camera**, not just asserted. Comment out `builder.Services.AddHealthChecks();`
and run the suite:

```
Failed!  - Failed: 1, Passed: 0, Skipped: 0, Total: 1

System.InvalidOperationException: Unable to resolve service for type
'Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService'...
   at Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory`1.CreateClient()
   at BrickShare.Catalog.IntegrationTests.HealthChecks.LivenessTests.Live_returns_ok()
```

The test fails the moment the factory tries to start the host — before the request is even
sent. That's the failure mode a unit test structurally cannot see, made visible, then reverted
before committing. It's the same teaching move as episode 3's broken health check: break it,
show the red, put it back.

## The project

```bash
dotnet new xunit -o tests/BrickShare.Catalog.IntegrationTests -n BrickShare.Catalog.IntegrationTests
dotnet sln BrickShare.slnx add tests/BrickShare.Catalog.IntegrationTests
dotnet add tests/BrickShare.Catalog.IntegrationTests reference src/Catalog/BrickShare.Catalog.Api
dotnet add tests/BrickShare.Catalog.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
```

**xUnit**, not because the others are wrong, but because it's the .NET default and the one
with the least ceremony — no `[TestClass]`/`[TestMethod]` pair to explain, just `[Fact]`.

The template drops in `UnitTest1.cs` with an empty `Test1()`. Delete it immediately — a
scaffold placeholder left in the repo teaches the same bad habit as the weather-forecast
endpoint in episode 2.

### `BrickShare.Catalog.IntegrationTests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj" />
  </ItemGroup>

</Project>
```

**One new package beyond the xUnit scaffold: `Microsoft.AspNetCore.Mvc.Testing`.** It brings
`WebApplicationFactory`, which hosts the real API in-process — the actual `Program.cs`, the
actual DI container, an actual (if in-memory) HTTP pipeline. Nothing about the app is faked.

The path in `ProjectReference` was generated with backslashes
(`..\..\src\Catalog\...`) by the SDK tooling on this machine — a Windows artifact. Normalized to
forward slashes here so the file doesn't look machine-specific to whoever clones it next; MSBuild
accepts either, but a course repo should not read like it was only ever built on one OS.

### Making `Program` visible to the test project

`Program.cs` uses top-level statements, which the compiler wraps in an **internal** generated
`Program` class — invisible outside the assembly, including to `WebApplicationFactory<Program>`
in the test project. One line at the end of the file fixes it:

```csharp
// Exposes the generated entry point so WebApplicationFactory<Program> can find it in tests.
public partial class Program;
```

This is boilerplate every ASP.NET Core project using top-level statements plus
`WebApplicationFactory` needs, and it's worth naming as exactly that on camera — not a design
decision, a workaround for how the compiler's sugar and the testing library interact.

## The test

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BrickShare.Catalog.IntegrationTests.HealthChecks;

public class LivenessTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Live_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

**Folder mirrors the thing under test**, not the test framework: `HealthChecks/LivenessTests.cs`,
not a flat pile of `*Tests.cs` files at the project root. This is the arrangement the rest of
the course follows — one folder per feature area, growing alongside `src/`.

**`IClassFixture<WebApplicationFactory<Program>>`** shares one host across every test in the
class instead of booting a fresh one per test. Booting a host is not free, and a suite that
starts one per `[Fact]` gets slow exactly when it has enough tests to matter. The constructor
parameter is xUnit's primary-constructor injection — the factory is handed in, not created.

**Test naming**: `MethodOrBehaviour_Condition_Expectation`, here shortened to
`Live_returns_ok` because there's no condition to vary yet. The pattern is introduced now so
episode 11's domain tests — which will have real conditions — have a convention already in
place rather than inventing one under pressure.

## What this episode deliberately does not do

- **No test for `/health/ready`.** It exercises the same `MapHealthChecks` code path as
  `/health/live` and would be testing the framework, not this project. It earns its own test in
  episode 15, once a real dependency check exists to fail meaningfully.
- **No coverage threshold, no coverage report even.** `coverlet.collector` ships with the xUnit
  template and sits unused. Measuring coverage on one test is theatre; it starts meaning
  something in episode 10, once there's a body of tests for a number to describe.
- **No custom `WebApplicationFactory` subclass**, no test configuration override, no fake
  dependencies. There's exactly one dependency-free endpoint to call. Building factory
  customization for a service that doesn't need it yet is the same mistake as scaffolding four
  projects in episode 2 — structure ahead of need.

## Verification

```bash
dotnet build       # 0 warnings, 0 errors, two projects
dotnet test        # 1 passed
```

To see the test earn its keep, comment out `builder.Services.AddHealthChecks();` in
`Program.cs`, run `dotnet test` again, watch it fail with the `HealthCheckService` resolution
error above, then put the line back.

## Next

Episode 5 — One image, run locally: a multi-stage `Dockerfile` and a `docker-compose.yml` with
a single service, so the container that ships is the container that's tested.
