# Episode 25 — Talking to Rebrickable

← [Course plan](catalog-api.md) · Previous: [Episode 24 — Two people, one Titanic](episode-24.md)

Five episodes built an endpoint that believes whatever it is told. UC-1.1 says it should not have to:

> Staff type a LEGO set number. BrickShare looks it up on Rebrickable and **prefills the form** with
> name, year, theme, piece count, image and the set's **minifigures**.

This episode builds the thing that does the looking up, and nothing else calls it yet. That is
deliberate: a network call to somebody else's server is the first component in this service that can
be **slow, flaky or gone**, and those three failure modes deserve their own video rather than a
paragraph inside the episode that wires up an endpoint.

**Done when** the client fetches a set over real HTTP against a stub, returns `null` for a set
number Rebrickable has never heard of, and survives two consecutive `500`s because of one line of
registration.

## Before recording

- Episode 24 merged: three gates, four tests, the `.http` file.
- A branch. No `docker compose up` needed — nothing here touches Postgres.
- A free [Rebrickable account](https://rebrickable.com/api/) and its API key, for the one manual
  call at the end. Everything before that runs offline.
- `https://rebrickable.com/api/v3/docs/` open in a tab, at `GET /lego/sets/{set_num}/`.

**Steps 2 to 5 are red-green.** Step 1 is test infrastructure and step 6 is configuration; both say
so rather than pretending to be driven by anything.

Every sample below names its file and where in it the code goes. Where something is added to a
file that already exists, the **first block is what is already there** — the anchor to find on
screen — and the **second block is what to paste**. Every block is copy-paste clean: no markers,
no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/Rebrickable/RebrickableStub.cs` | New |
| `tests/…/Rebrickable/RebrickableClientTests.cs` | New |
| `src/…/Api/Rebrickable/RebrickableOptions.cs` | New |
| `src/…/Api/Rebrickable/RebrickableSet.cs` | New |
| `src/…/Api/Rebrickable/RebrickableSetPayload.cs` | New |
| `src/…/Api/Rebrickable/IRebrickableCatalog.cs` | New |
| `src/…/Api/Rebrickable/RebrickableClient.cs` | New |
| `src/…/Api/Rebrickable/RebrickableServiceCollectionExtensions.cs` | New |
| `src/…/Api/Program.cs` | One line |
| `tests/…/CatalogApiFactory.cs` | The dictionary, the signature and the comment |
| `tests/…/HealthChecks/LivenessTests.cs` | Rewritten — see step 3 |
| `Directory.Packages.props` · `BrickShare.Catalog.Api.csproj` | One line each |
| `src/…/Api/appsettings.json` · `docker-compose.yml` | Configuration |

---

## Step 1 — Rebrickable, on loopback

Before a test can drive the client, something has to answer it. The choice made here is the one the
rest of the episode rests on, so it is worth stating before the code:

> **The tests stub the server, not the client.** No fake `HttpMessageHandler`, no second
> implementation of the interface behind a flag. A real socket, real JSON, real status codes — and
> the *real* client, registered by the *real* registration code. **Configuration is the only thing
> that differs between this and production.**

The alternative — an in-memory handler, or a `StubRebrickableCatalog` selected by config — is
faster to write and tests less. A swapped implementation proves nothing about the client, and a fake
handler skips the pipeline that the second half of this episode is entirely about.

**New file**, in a new folder: `tests/BrickShare.Catalog.IntegrationTests/Rebrickable/RebrickableStub.cs`.
It needs no package — `Microsoft.AspNetCore.Mvc.Testing` already brings the `Microsoft.AspNetCore.App`
framework reference that `WebApplication` lives in, which is worth pointing at rather than leaving
as a mystery.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.Logging;

namespace BrickShare.Catalog.IntegrationTests.Rebrickable;

/// <summary>
/// Rebrickable, as far as the client can tell. Test infrastructure, so it is written outright
/// rather than driven by a test of its own — it has no behaviour we would ever have to defend.
/// </summary>
public sealed class RebrickableStub : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _failuresRemaining;

    private RebrickableStub()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        // Port 0 means "whatever is free". Two test classes running in parallel must not fight
        // over a hard-coded port, and CI is where that fight would happen.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();

        _app.MapGet("/api/v3/lego/sets/{setNumber}/", (string setNumber, HttpRequest request) =>
        {
            Requests++;
            LastAuthorization = request.Headers.Authorization.ToString();

            if (_failuresRemaining > 0)
            {
                _failuresRemaining--;
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Sets.TryGetValue(setNumber, out object? payload)
                ? Results.Json(payload)
                : Results.NotFound();
        });
    }

    public Dictionary<string, object> Sets { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Requests { get; private set; }

    public string? LastAuthorization { get; private set; }

    public string BaseAddress => $"{_app.Urls.First()}/api/v3/";

    public void FailNextRequests(int count) => _failuresRemaining = count;

    /// <summary>
    /// Constructing and starting are one step, so there is no such thing as a stub that exists
    /// but is not listening — and so a test can hold it in a single `await using`.
    /// </summary>
    public static async Task<RebrickableStub> StartAsync()
    {
        var stub = new RebrickableStub();
        await stub._app.StartAsync();

        return stub;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
```

Three knobs, one per thing the episode needs to prove: a canned payload, a failure counter, and a
record of what arrived in the `Authorization` header.

---

## Step 2 — Red: a client that does not exist

**New file:** `tests/BrickShare.Catalog.IntegrationTests/Rebrickable/RebrickableClientTests.cs`,
next to the stub. Write the shell first — the service provider each test builds and the payload
helper — because every test in the episode lands inside it:

```csharp
using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Catalog.IntegrationTests.Rebrickable;

/// <summary>
/// No database, so no <see cref="DatabaseCollection"/>: this class runs in parallel with
/// everything else, and every test starts a stub server of its own.
/// </summary>
public sealed class RebrickableClientTests
{
    private static ServiceProvider Services(RebrickableStub rebrickable) =>
        new ServiceCollection()
            .AddRebrickable(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rebrickable:BaseAddress"] = rebrickable.BaseAddress,
                    ["Rebrickable:ApiKey"] = "test-key"
                })
                .Build())
            .BuildServiceProvider();

    private static object Titanic() => new
    {
        set_num = "10294-1",
        name = "Titanic",
        year = 2021,
        theme_id = 252,
        num_parts = 9090,
        set_img_url = "https://cdn.rebrickable.com/media/sets/10294-1.jpg"
    };
}
```

**A stub per test, not a stub per class**, and the compiler has an opinion about the alternative.
An `IAsyncLifetime` fixture holding the stub in a field builds red under this repository's
warnings-as-errors:

```
error CA1001: Type 'RebrickableClientTests' owns disposable field(s) '_rebrickable'
but is not disposable
```

The analyzer cannot see that xunit calls `DisposeAsync` for it, so it is wrong about the leak and
**right about the ownership** — a field whose lifetime is managed by a framework the compiler cannot
see is a field worth not having. Scoping the stub to each test answers the warning by removing the
field, and pays for itself twice over: every test gets a fresh `Requests` counter, which step 5
depends on, and nothing is shared between tests that run in any order.

**Every `[Fact]` in this episode goes in the same place**: inside the class, above `Services()`.
The first one:

```csharp
    [Fact]
    public async Task A_known_set_comes_back_with_the_facts_staff_do_not_type()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();
        rebrickable.Sets["10294-1"] = Titanic();

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        RebrickableSet? set = await catalog.FindSetAsync(
            SetNumber.Parse("10294-1"), CancellationToken.None);

        Assert.NotNull(set);
        Assert.Equal("Titanic", set.Name);
        Assert.Equal(2021, set.Year);
        Assert.Equal(9090, set.PieceCount);
    }
```

```
error CS0246: The type or namespace name 'IRebrickableCatalog' could not be found
error CS1061: 'IServiceCollection' does not contain a definition for 'AddRebrickable'
```

**A build error against a type that does not exist yet is the red.** Note what the test already
fixed before a line of production code exists: the shape of the call (`FindSetAsync`, a `SetNumber`,
a cancellation token), the shape of the answer (`RebrickableSet?`, our names, not theirs) and the
name of the configuration section.

---

## Step 3 — Green: the smallest client that fetches a set

Six new files, all in one new folder: `src/Catalog/BrickShare.Catalog.Api/Rebrickable/`. Nothing
here is edited into an existing file except the last two snippets of the step.

**New file:** `Rebrickable/RebrickableOptions.cs`.

```csharp
using System.ComponentModel.DataAnnotations;

namespace BrickShare.Catalog.Api.Rebrickable;

public sealed class RebrickableOptions
{
    public const string SectionName = "Rebrickable";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://rebrickable.com/api/v3/");

    /// <summary>
    /// Required, so an instance with no key refuses to start rather than discovering the problem
    /// on the first staff lookup. A misconfigured deployment should fail the health probe, not a
    /// staff member's afternoon.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;
}
```

**Two records, in two new files, on purpose** — the episode's second-smallest idea and worth saying
out loud.

`Rebrickable/RebrickableSet.cs`:

```csharp
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Rebrickable;

/// <summary>What the rest of BrickShare wants. Our names, our types.</summary>
public sealed record RebrickableSet(
    SetNumber Number,
    string Name,
    int Year,
    int ThemeId,
    int PieceCount,
    Uri? ImageUrl);
```

`Rebrickable/RebrickableSetPayload.cs`:

```csharp
using System.Text.Json.Serialization;

namespace BrickShare.Catalog.Api.Rebrickable;

/// <summary>What Rebrickable actually sends. Their names, and they are not ours to choose.</summary>
internal sealed record RebrickableSetPayload(
    [property: JsonPropertyName("set_num")] string SetNumber,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("theme_id")] int ThemeId,
    [property: JsonPropertyName("num_parts")] int PieceCount,
    [property: JsonPropertyName("set_img_url")] Uri? ImageUrl);
```

It is one more record than strictly necessary, and it buys a real thing: **a third party's field
names must not become ours.** `num_parts` would otherwise travel from their JSON into our entity,
our database column, our API response and our OpenAPI document, and the day Rebrickable renames it
the cost is a migration rather than one attribute. The payload type is `internal` because nothing
outside this folder should ever see it.

**New file:** `Rebrickable/IRebrickableCatalog.cs`.

```csharp
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Rebrickable;

public interface IRebrickableCatalog
{
    Task<RebrickableSet?> FindSetAsync(SetNumber number, CancellationToken cancellationToken);
}
```

**New file:** `Rebrickable/RebrickableClient.cs`.

```csharp
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Rebrickable;

// System.Net.Http.Json is an implicit using in the Web SDK, so ReadFromJsonAsync needs no
// directive here — and adding one fails the build on IDE0005.
internal sealed class RebrickableClient(HttpClient http) : IRebrickableCatalog
{
    public async Task<RebrickableSet?> FindSetAsync(
        SetNumber number, CancellationToken cancellationToken)
    {
        // Relative, and with no leading slash. A leading slash would discard the "/api/v3/" in
        // BaseAddress and ask rebrickable.com for "/lego/sets/..." — a 404 that looks like a
        // missing set. This is the single most common typed-client bug there is.
        using HttpResponseMessage response = await http.GetAsync(
            $"lego/sets/{Uri.EscapeDataString(number.Value)}/", cancellationToken);

        response.EnsureSuccessStatusCode();

        RebrickableSetPayload payload =
            await response.Content.ReadFromJsonAsync<RebrickableSetPayload>(cancellationToken)
            ?? throw new InvalidOperationException($"Rebrickable sent an empty body for set {number}.");

        // Their set number, not the one staff typed. Staff type "10294"; the catalogue is keyed on
        // "10294-1", and Rebrickable is the authority on its own identifiers. First small instance
        // of the rule episode 27 is entirely about.
        return new RebrickableSet(
            SetNumber.Parse(payload.SetNumber),
            payload.Name,
            payload.Year,
            payload.ThemeId,
            payload.PieceCount,
            payload.ImageUrl);
    }
}
```

**New file:** `Rebrickable/RebrickableServiceCollectionExtensions.cs` — wiring, so not test-driven,
but not untested either, because every test in this episode reaches the client through it.

```csharp
using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

namespace BrickShare.Catalog.Api.Rebrickable;

public static class RebrickableServiceCollectionExtensions
{
    public static IServiceCollection AddRebrickable(
        this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(RebrickableOptions.SectionName);

        services.AddOptions<RebrickableOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IRebrickableCatalog, RebrickableClient>((sp, client) =>
        {
            RebrickableOptions options =
                sp.GetRequiredService<IOptions<RebrickableOptions>>().Value;

            client.BaseAddress = options.BaseAddress;

            // Rebrickable's own scheme: "Authorization: key <the key>".
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("key", options.ApiKey);
        });

        return services;
    }
}
```

`AddHttpClient<TInterface, TImplementation>` is what makes this a **typed client**: the handler and
its connection pool are managed for us and rotated on a schedule, and the class receives an
`HttpClient` that is already pointed at the right host with the right headers. A `new HttpClient()`
in a field is the socket-exhaustion bug; a `new HttpClient()` per call is the same bug wearing a
disguise.

### Three existing files, and the bug hiding in the second one

`src/Catalog/BrickShare.Catalog.Api/Program.cs`, around line 60. Already there:

```csharp
builder.Services.AddDbContext<CatalogDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

builder.Services.AddExceptionHandler<DomainRuleViolationExceptionHandler>();
```

Paste between those two, and add `using BrickShare.Catalog.Api.Rebrickable;` to the using block at
the top of the file:

```csharp
builder.Services.AddRebrickable(builder.Configuration);
```

Green — but not yet, for a reason worth showing on camera. `ValidateOnStart` now fails every
integration test that boots the API, because the test host has no key:

```
OptionsValidationException: DataAnnotation validation failed for 'RebrickableOptions'
members: 'ApiKey' with the error: 'The ApiKey field is required.'
```

The obvious fix is one line, in the factory that already exists to supply the configuration a test
host cannot discover for itself. It is the right kind of fix, and — as the next two minutes show — it
is not the whole one.

`tests/BrickShare.Catalog.IntegrationTests/CatalogApiFactory.cs`. Already there:

```csharp
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = connectionString
            }));
```

Replace that dictionary with this one — the existing line gains a comma:

```csharp
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = connectionString,
                ["Rebrickable:ApiKey"] = "not-used-by-these-tests"
            }));
```

### The second red: one test did not get the memo

Run the suite again. Everything that goes through that factory is green, and one test is not:

```
Failed BrickShare.Catalog.IntegrationTests.HealthChecks.LivenessTests.Live_returns_ok
Error Message:
 Microsoft.Extensions.Options.OptionsValidationException : DataAnnotation validation failed for
 'RebrickableOptions' members: 'ApiKey' with the error: 'The ApiKey field is required.'.
```

Two lines explain it. The factory we just fixed, in `CatalogApiFactory.cs`:

```csharp
public sealed class CatalogApiFactory(string connectionString) : WebApplicationFactory<Program>
```

And `tests/BrickShare.Catalog.IntegrationTests/HealthChecks/LivenessTests.cs`, which has never heard
of it:

```csharp
public class LivenessTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
```

**Two factories boot this application, and they disagree about what it needs in order to start.**
That is the defect, and it is worth naming before the fix, because the tempting repair — a second
`["Rebrickable:ApiKey"]` literal, in a second place — leaves the defect exactly where it is, waiting
for the next setting to find it. There will be more configuration: a vault URI in episode 26, a
storage account in episode 34, an Entra tenant in episode 36. The first of those that the application
refuses to start without would find this same gap again, in the same place, for the same reason.

**Episode 4 was not wrong**, and this is the distinction to draw carefully. That test was written to
depend on nothing, and it still depends on nothing — the probe runs no checks, touches no database,
and `Predicate = _ => false` is still the whole implementation. What changed is that **starting the
process** now requires a secret, which is a different claim from **answering the probe** requiring
one. A liveness test cannot assert the first without satisfying it.

So: one factory. The connection string is the only thing that varies between the callers, so it is
the only thing that stays a parameter.

`tests/BrickShare.Catalog.IntegrationTests/CatalogApiFactory.cs`, the declaration and the comment
above it. Already there:

```csharp
/// <summary>
/// The API, wired to the test container. It overrides one configuration key and nothing else:
/// every service registration in Program.cs is the one that runs in production.
/// </summary>
public sealed class CatalogApiFactory(string connectionString) : WebApplicationFactory<Program>
```

Becomes — note that the comment was already out of date the moment the dictionary above gained a
second entry, which is its own small lesson about comments that count things:

```csharp
/// <summary>
/// The API, booted with the configuration it cannot start without. Tests that need a database pass
/// a connection string; tests that do not, pass nothing. Every service registration in Program.cs
/// is the one that runs in production.
/// </summary>
public sealed class CatalogApiFactory(string? connectionString) : WebApplicationFactory<Program>
```

`tests/BrickShare.Catalog.IntegrationTests/HealthChecks/LivenessTests.cs`, replaced entirely — it
loses a `using`, a fixture and a type parameter, and gains nothing:

```csharp
using System.Net;

namespace BrickShare.Catalog.IntegrationTests.HealthChecks;

public class LivenessTests
{
    [Fact]
    public async Task Live_returns_ok()
    {
        await using CatalogApiFactory api = new(connectionString: null);

        HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

Green — the liveness test included, and for the same reason as everything else.

Two small things in that file are deliberate. **`connectionString: null` is named**, because an
argument that says *liveness has no database* is better than a bare `null` a reader has to go and look
up — and it is the assertion this test is quietly making: the app starts, and answers, with no
database configured at all. **And `await using` replaces the class fixture**, which is the same
argument step 2 made about the Rebrickable stub: one test, one instance, and no field whose lifetime
is owned by a framework the compiler cannot see. One test does not need a shared host.

### Why this one is easy to ship

Following this script in order, that failure showed up locally, immediately, which is the cheapest
place a failure can be. It is worth thirty seconds on the version where it does not.

Set the key in user secrets first — which is what happens the second time anybody builds this, and
what happened while this episode was being written — and `Live_returns_ok` **passes on your machine
and fails in the pipeline.** `WebApplicationFactory` boots the application in the **Development**
environment, `WebApplication.CreateBuilder` adds the user-secrets provider in Development, and your
user secrets have the key in them. The test was green because of a file in your home directory that
is in no repository and on no other computer.

You can watch that happen without pushing anything:

```bash
dotnet user-secrets list --project src/Catalog/BrickShare.Catalog.Api
dotnet user-secrets remove "Rebrickable:ApiKey" --project src/Catalog/BrickShare.Catalog.Api

dotnet test tests/BrickShare.Catalog.IntegrationTests

dotnet user-secrets set "Rebrickable:ApiKey" "<your key>" --project src/Catalog/BrickShare.Catalog.Api
```

With the fix above, that sequence stays green throughout — no test reads your secrets. Without it,
the middle command is the CI failure, reproduced on a laptop in four seconds.

**A test that passes because of an untracked file in your home directory is a test that is lying to
you**, and the only thing that reliably catches it is a machine with nothing on it. That machine is
what episodes 9 to 11 were for, and this is the first episode in which it earns its keep by finding
something a human would not have.

**Startup validation earning itself within ten minutes of being written** is worth naming, and it has
now done it twice: once in a build, telling us a new required setting exists, and once in a test,
telling us two fixtures disagreed about it. Without it, the first of those discoveries happens in
Azure, on the first lookup, as a 500 — and the second one never happens at all.

---

## Step 4 — A 404 is an answer, not a failure

`tests/BrickShare.Catalog.IntegrationTests/Rebrickable/RebrickableClientTests.cs`, directly below
the first test:

```csharp
    [Fact]
    public async Task A_set_Rebrickable_has_never_heard_of_is_not_found_rather_than_broken()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        RebrickableSet? set = await catalog.FindSetAsync(
            SetNumber.Parse("99999-1"), CancellationToken.None);

        Assert.Null(set);
    }
```

Red, from the `EnsureSuccessStatusCode` written in step 3:

```
HttpRequestException : Response status code does not indicate success: 404 (Not Found).
```

Green is one `if`. `Rebrickable/RebrickableClient.cs` — already there:

```csharp
        using HttpResponseMessage response = await http.GetAsync(
            $"lego/sets/{Uri.EscapeDataString(number.Value)}/", cancellationToken);

        response.EnsureSuccessStatusCode();
```

Paste between those two statements, and add `using System.Net;` at the top of the file:

```csharp
        // A typo is the most likely thing a staff member does at this endpoint, and it must not
        // arrive at the handler looking like a Rebrickable outage. Those two need different words
        // on screen, so they get different return values here.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
```

**`EnsureSuccessStatusCode` is exactly wrong for the expected case and exactly right for the rest.**
404 is the server answering the question. Everything else 4xx or 5xx is the server failing to, and
that stays an exception — episode 27 turns it into a `502`/`503` with a sentence staff can act on.

---

## Step 5 — Red: two failures and the call gives up

`RebrickableClientTests.cs`, below the 404 test:

```csharp
    [Fact]
    public async Task A_transient_failure_is_retried_rather_than_surfaced()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();
        rebrickable.Sets["10294-1"] = Titanic();
        rebrickable.FailNextRequests(2);

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        RebrickableSet? set = await catalog.FindSetAsync(
            SetNumber.Parse("10294-1"), CancellationToken.None);

        Assert.NotNull(set);
        Assert.Equal(3, rebrickable.Requests);
    }
```

```
HttpRequestException : Response status code does not indicate success: 500 (Internal Server Error).
```

Green is a package and a line.

`Directory.Packages.props`, in the single `ItemGroup`. Already there:

```xml
    <PackageVersion Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.11" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
```

Paste between them — the list is alphabetical and stays that way:

```xml
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="10.10.0" />
```

`src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj`, in the second `ItemGroup`.
Already there:

```xml
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
```

Paste between them — no version attribute, because central package management owns versions:

```xml
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
```

`Rebrickable/RebrickableServiceCollectionExtensions.cs`, at the end of the `AddHttpClient` call.
Already there:

```csharp
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("key", options.ApiKey);
        });
```

Becomes — the `});` loses its semicolon and the handler chains onto it:

```csharp
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("key", options.ApiKey);
        })
        .AddStandardResilienceHandler(section.GetSection("Resilience"));
```

No new `using`: `AddStandardResilienceHandler` is an extension on `IHttpClientBuilder` in
`Microsoft.Extensions.DependencyInjection`, which the Web SDK's implicit usings already cover.

**That overload reads the pipeline's settings from configuration**, which means the settings have a
home — so the section goes into `appsettings.json` next to the base address, as a new top-level
object after `AllowedHosts`, which picks up a comma:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Rebrickable": {
    "BaseAddress": "https://rebrickable.com/api/v3/",
    "Resilience": {
      "Retry": {
        "Delay": "00:00:02"
      }
    }
  }
}
```

That `Delay` is the library's own default, written out explicitly. **A default you have typed is a
default you can change without a deployment** — and it is the one knob this episode is about to turn.

There is deliberately **no `ApiKey` line here, not even an empty one**: a placeholder in a committed
file is an invitation to fill it in and commit it. Step 6 deals with the key.

Green, and slowly — that one test takes about six seconds, because the first retry waits two
seconds and the second waits four. **Configuration is the only difference between a test and
production**, and that is exactly what the `section.GetSection("Resilience")` overload is for.

`RebrickableClientTests.cs`, in the in-memory configuration inside `Services()`. Already there:

```csharp
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rebrickable:BaseAddress"] = rebrickable.BaseAddress,
                    ["Rebrickable:ApiKey"] = "test-key"
                })
```

Becomes:

```csharp
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rebrickable:BaseAddress"] = rebrickable.BaseAddress,
                    ["Rebrickable:ApiKey"] = "test-key",
                    // Production waits two seconds before the first retry. A test that waits two
                    // seconds is a test somebody eventually deletes.
                    ["Rebrickable:Resilience:Retry:Delay"] = "00:00:00.001"
                })
```

Green, in milliseconds, with the production retry strategy — the same count, the same backoff
curve, the same set of conditions it retries on. Only the clock moved.

### What the five strategies are for

`AddStandardResilienceHandler` is five Polly strategies, outermost first, and the ordering is the
interesting part:

| | Default | What it is protecting |
| --- | --- | --- |
| **Rate limiter** | 1 000 concurrent, no queue | **Us.** A dependency that slows to a crawl otherwise parks every thread-pool thread we have on a socket wait. This is the strategy that stops *their* outage becoming *our* outage. |
| **Total timeout** | 30 s | **The caller.** A staff member is watching a spinner. Thirty seconds is the whole budget, retries included. |
| **Retry** | 3 retries, exponential backoff with jitter from a 2 s base, on 408, 429, 5xx, `HttpRequestException` and timeouts | **The request.** Most transient failures are over in a second and the caller never learns one happened. |
| **Circuit breaker** | opens at a 10 % failure ratio over a 30 s window, minimum 100 requests, stays open 5 s | **Them, and us.** When a dependency is genuinely down, every call fails instantly instead of occupying a connection for ten seconds first. |
| **Attempt timeout** | 10 s | **Each try.** Without it a single hung socket eats the whole 30 s budget and the retries never happen. |

The sentence this episode exists for is the third and fourth rows together: **retry without a
circuit breaker turns a slow dependency into a self-inflicted outage.** Rebrickable gets slow, every
one of our requests retries three times, and we have just tripled the load on the service least able
to absorb it — while our own threads sit waiting. Retry assumes the failure is rare. The breaker is
what notices that it has stopped being rare.

Two more details worth thirty seconds each:

- **`HttpClient.Timeout` is still 100 seconds and sits *outside* the pipeline.** The 30-second total
  timeout is what actually fires. Leave the two consistent or one of them is a lie.
- **Retry is safe here because this is a `GET`.** For a non-idempotent call the library ships
  `retryOptions.DisableForUnsafeHttpMethods()`, and knowing that it exists is the point — a retried
  `POST` to a third party is a duplicate order.

### What is not tested here, and why

**The circuit breaker.** Tripping it needs 100 requests inside a 30-second window, so a test that
opens it would either rewrite those defaults into something meaningless or spend half a minute
proving that Polly works. **That is a test about the library, not about BrickShare.** The defaults
go on screen instead, and the breaker's real proof arrives in episode 37, when the traces show it
opening.

Both halves of that are worth saying plainly: a test that pins a third party's defaults is noise,
and *pretending* the breaker is covered would be worse than admitting it is not.

**The key travels, though**, because that is our code and it is one assertion.
`RebrickableClientTests.cs`, below the retry test and still above `Services()`:

```csharp
    [Fact]
    public async Task Every_request_carries_the_api_key()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();
        rebrickable.Sets["10294-1"] = Titanic();

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        await catalog.FindSetAsync(SetNumber.Parse("10294-1"), CancellationToken.None);

        Assert.Equal("key test-key", rebrickable.LastAuthorization);
    }
```

It passes the moment it is written, and it stays — `CLAUDE.md` is explicit that some tests drive a
design and some describe a rule. This one describes a rule, and **episode 26 is going to change
where that key comes from**. The test that does not change is how we will know the swap worked.

---

## Step 6 — The key, and the debt it leaves

The base address and the retry delay went into `appsettings.json` in step 5, and neither is a
secret. The key is, and it belongs nowhere near the repository. Run these from the API project
folder — `user-secrets init` writes a `UserSecretsId` into the `.csproj`, which *is* committed and
is only an identifier:

```bash
cd src/Catalog/BrickShare.Catalog.Api
dotnet user-secrets init
dotnet user-secrets set "Rebrickable:ApiKey" "<your key>"
```

User secrets live in the user profile, outside the working tree, and are read automatically in
Development. Compose is a container and has no user profile, so it takes the key from the
environment.

**"Read automatically in Development" is also read automatically by the tests**, which is the trap
step 3 spent thirty seconds on. From this command onwards, the test suite would pass on this machine
whether or not `CatalogApiFactory` supplies a key. It is the fix from step 3 that keeps that from
being true, and it is the reason the fix was to have one factory rather than two literals.

`docker-compose.yml`, in the `catalog-api` service's `environment` block. Already there, as the
last key in it:

```yaml
      ConnectionStrings__Catalog: "Host=postgres;Port=5432;Database=brickshare_catalog;Username=brickshare;Password=brickshare"
```

Paste below it:

```yaml
      # From .env, which .gitignore already covers. A key committed once is a key that has to be
      # rotated, and rotating somebody else's key means an email and a wait.
      Rebrickable__ApiKey: ${REBRICKABLE_API_KEY}
```

and a `.env` beside it, untracked:

```
REBRICKABLE_API_KEY=<your key>
```

**And now say the quiet part**, because it is the hook for the next episode: nothing above works in
Azure. App Service application settings would — and would put the key in Terraform state, in the
portal for anyone with Contributor, and in a `terraform plan` diff on every pull request. Every
credential this service has used so far has been a **managed identity**: Postgres in episode 18, the
registry in episode 8, and no password anywhere. That streak ends here, because Rebrickable will
never federate with our tenant. **This is the first genuine secret in the system**, and it needs
somewhere to live that is not a configuration value. That is episode 26.

---

## What this episode is not

**No `POST /catalog/lookups`.** The client has no caller, and that is the boundary this episode
drew on purpose. Episode 27 gives it one, along with the snapshot, the image copy and the checklist.

**No readiness check for Rebrickable**, and this is a decision rather than an omission. Tagging it
`ready` would take an instance out of rotation during a Rebrickable outage — an instance that can
still browse the catalog, register copies, retire them, regrade them and take a rental. **Only
cataloguing a never-stocked set stops working.** A readiness probe answers "can this instance serve
traffic", not "is every dependency healthy", and the difference between those two questions is the
difference between a degraded feature and an outage.

**No caching, and none needed for the blast radius.** The facts are fetched once and stored at
catalogue time, so the shop's second Titanic makes no external call at all. That is not a cache —
it is the data model, and it is why a Rebrickable outage costs so little.

**No `rebrickable-stub` container in Compose.** `docs/architecture/catalog.md` lists one and it is
still coming; it earns its place in episode 27, when there is an endpoint to call it through.
Adding it now would be a container nothing talks to.

**No error mapping.** A Rebrickable 500 that survives the retries currently surfaces as an unhandled
`HttpRequestException`, which the episode-23 handler does not recognise, which means a `500`. That
is honest — we *are* broken in that moment — but "Rebrickable is unavailable, try again shortly" is
a better sentence and it needs an endpoint to be said from. Episode 27.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green — the four new client tests, and every test that was green before |
| `dotnet test` wall clock | No slower than before; the retry test runs in milliseconds |
| Remove `.AddStandardResilienceHandler(…)` | Only the retry test fails. Put it back |
| Remove `["Rebrickable:ApiKey"]` from `CatalogApiFactory` | Every API test fails at startup, by design — `Live_returns_ok` included |
| `dotnet user-secrets remove "Rebrickable:ApiKey"`, then `dotnet test` | Still green. No test reads your home directory. Set it back afterwards |
| `git grep -n "WebApplicationFactory<Program>" -- tests` | One hit: `CatalogApiFactory`'s own declaration |
| A real call with a real key | `GET https://rebrickable.com/api/v3/lego/sets/10294-1/` returns the Titanic |
| The same call with the key removed | `401`, fast — not a hang, and not a retry storm |
| `git grep -i "rebrickable" -- '*.json'` | The base address, and no key |

## Next

[Episode 26 — The first real secret](episode-26.md): the key
this episode left in configuration goes into **Key Vault**, read through the app's managed identity,
with the Terraform and the role assignment that make that work — and the "every request carries the
API key" test from step 5 stays untouched, which is how we will know nothing else changed.

Then episode 27 takes the request body episodes 20 to 24 built and deletes most of it, because
**the server now has its own source for those facts** and a client that can still send them is an
authorization bug.
