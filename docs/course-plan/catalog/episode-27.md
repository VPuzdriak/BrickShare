# Episode 27 — The lookup that owns the facts

← [Course plan](catalog-api.md) · Previous: [Episode 26 — The first real secret](episode-26.md)

Episode 25 built a client that talks to Rebrickable. Episode 26 gave it a key that survives a
deployment. Neither episode gave it a **caller**: `IRebrickableCatalog` is registered, tested,
resilient, funded by a Key Vault secret — and nothing in the application has ever asked it a
question.

Meanwhile `POST /catalog/sets` accepts nine fields, and five of them are product facts the person
calling has no business deciding.

This episode builds the endpoint that fixes that: `POST /catalog/lookups` asks Rebrickable, stores
what it said, and hands back a draft. **Episode 28 is the half that takes the fields away.** The
security argument runs across both, which is unusual and worth saying out loud on camera — this
episode builds the thing create will be made to depend on.

**Done when** a lookup on `10294-1` returns a prefilled draft with a `lookupId`, a row exists in
`rebrickable_snapshots` holding facts no client sent, and a set number Rebrickable has never heard
of comes back as a `404` rather than an outage.

> **Runtime: about 14 minutes.** The step most likely to overrun is step 3, which is test
> infrastructure and nothing else — if it does, the second close call in step 6 is the one to cut.

## Before recording

- Episode 26 merged: Key Vault, the configuration provider, a green deploy.
- `docker compose up` for Postgres, and `dotnet user-secrets` still holding your Rebrickable key.
- A branch.
- `dotnet ef` available on the path — this episode adds a migration.

**Two of the seven steps are not driven by a test**, and the script says which as it reaches them:
step 3 is test infrastructure, step 5 is persistence mapping and a generated migration. `CLAUDE.md`
names both as legitimately test-free. Everything else here goes red first.

Every sample below names its file and where in it the code goes. Where something is added to a file
that already exists, the **first block is what is already there** — the anchor to find on screen —
and the **second block is what to paste**. Every block is copy-paste clean: no markers, no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/CatalogSets/LookupTests.cs` | **New** — four tests |
| `tests/…/CatalogDatabase.cs` · `CatalogApiFactory.cs` · `DatabaseTest.cs` | One stub, shared and reset |
| `tests/…/Rebrickable/RebrickableStub.cs` | A themes route, a `Reset` |
| `tests/…/Rebrickable/RebrickableClientTests.cs` | One new test |
| `src/…/Api/Rebrickable/RebrickableTheme.cs` · `RebrickableThemePayload.cs` | **New** |
| `src/…/Api/Rebrickable/IRebrickableCatalog.cs` · `RebrickableClient.cs` | One method |
| `src/…/Api/Rebrickable/RebrickableSnapshot.cs` | **New** |
| `src/…/Api/Persistence/RebrickableSnapshotConfiguration.cs` | **New** |
| `src/…/Api/Persistence/CatalogDbContext.cs` | One `DbSet` |
| `src/…/Api/Migrations/…_AddRebrickableSnapshots.cs` | Generated |
| `src/…/Api/Endpoints/CatalogLookupEndpoints.cs` · `LookupRequestValidator.cs` | **New** |
| `src/…/Api/Program.cs` | Three lines, plus a stale comment |
| `src/…/Api/BrickShare.Catalog.Api.http` | Four requests |

`CatalogSet`, `CatalogSetEndpoints` and `CatalogueSetRequestValidator` are **not opened**. That is
episode 28, on purpose.

---

## Step 1 — A `201` that invents a product

Start with the file we are *not* going to change, and the comment already sitting in it:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — near the bottom
/// <summary>
/// What staff send to catalogue a set. Every product fact in here is client-supplied, which is
/// a security problem episode 26 exists to fix.
/// </summary>
public sealed record CatalogueSetRequest(
    string SetNumber,
    string Name,
    string Theme,
    int Year,
    int PieceCount,
    decimal RetailPrice,
    decimal BaseRentalPrice,
    int MinimumRentalDays,
    int MinimumAge);
```

Two things to say about that comment. The first: it was written in episode 21 and it names the wrong
episode — the fix landed in 26 in the original plan, then this material split in two, and it is now
**episode 28**. Fix the number while it is on screen; a comment pointing at the wrong episode is how
a reader concludes the whole file is untrustworthy. There is a second one of these, and the same
thirty seconds fixes it: `Program.cs` says "Episode 34 wires the other end of this" about the
`traceId` extension, and observability is now **episode 39**.

The second is the actual point. Run this against a running API:

```bash
curl -i -X POST http://localhost:5080/api/v1/catalog/sets \
  -H "Content-Type: application/json" \
  -d '{ "setNumber": "99999-9", "name": "Definitely A Real Set", "theme": "Free Stuff",
        "year": 2021, "pieceCount": 4, "retailPrice": 0.01, "baseRentalPrice": 0.01,
        "minimumRentalDays": 1, "minimumAge": 0 }'
```

`201 Created`. BrickShare now sells a four-piece Titanic that LEGO never made, and every validator
episode 22 wrote passed, because **every one of those fields was well-formed.** Validation asks
*is this value sensible?* The question nobody asked is a different one:

> **Whose data is this?**

Ask it field by field down that record. Retail price: the shop's. Base rental price: the shop's.
Minimum rental days, minimum age: the shop's — an age rating is a judgement the shop makes about
its own liability, not a fact copied from a box. Name, theme, year, piece count: **LEGO's**, and
BrickShare learns them from Rebrickable. Set number: the client's to *ask about*, and nobody's to
invent.

Five fields with the wrong owner is not a validation gap. It is an authorization bug wearing an API
design costume, and no amount of `RuleFor` fixes it, because the value passed every rule. **A field
the client must not choose cannot be a field the client can send.**

That is why this is two endpoints. Lookup fetches the facts and stores them; create references
them. This episode is the first half.

---

## Step 2 — Red: an endpoint nobody wrote

The failing test first, in a new file.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/LookupTests.cs — new file
using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;

namespace BrickShare.Catalog.IntegrationTests.CatalogSets;

public class LookupTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_lookup_returns_the_facts_staff_do_not_type()
    {
        Database.Rebrickable.Sets["10294-1"] = Titanic();
        Database.Rebrickable.Themes[252] = Icons();

        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);
        Assert.NotEqual(Guid.Empty, draft.LookupId);
        Assert.Equal("Titanic", draft.Name);
        Assert.Equal("Icons", draft.Theme);
        Assert.Equal(9092, draft.PieceCount);
    }

    private static object Titanic() => new
    {
        set_num = "10294-1",
        name = "Titanic",
        year = 2021,
        theme_id = 252,
        num_parts = 9092,
        set_img_url = "https://cdn.rebrickable.com/media/sets/10294-1.jpg"
    };

    private static object Icons() => new { id = 252, name = "Icons", parent_id = (int?)null };
}
```

This does not compile, and it names four things that do not exist: `Database.Rebrickable`,
`Themes`, the route, and `LookupResponse`. **A build error is a legitimate red** — episode 12 made
that argument and it holds here: the test is a statement about a design that has not been written
yet, and the compiler is the fastest possible way to hear "no".

Worth pausing on what the test asserts and what it does not. It asserts the response carries a
`lookupId` and the three facts staff must not type. It does not assert the image, the year, or the
row in the database — those arrive in step 6's other tests. **One red at a time**, or the green step
becomes four features wide and you cannot tell which line made it pass.

---

## Step 3 — One stub for the whole run

Test infrastructure, written outright. No test drives it and this one is easy to justify: it has no
behaviour BrickShare would ever have to defend, and the thing it replaces is somebody else's server.

There is a real constraint behind the shape, and it is worth thirty seconds because it is the sort
of thing that reads as arbitrary later. `RebrickableClientTests` starts a stub per test, which is
perfect for it — no database, no shared state, full parallelism. An **endpoint** test cannot do
that. `CatalogApiFactory` is constructed once, in `CatalogDatabase.InitializeAsync`, before any test
runs; the stub's URL does not exist until the stub is listening. Configuration has to be known
before the host is built, and the address is only known after the stub starts.

So the database fixture — which already owns one expensive, long-lived thing — owns a second.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Rebrickable/RebrickableStub.cs — the constructor, as it is
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
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Rebrickable/RebrickableStub.cs — add the themes route
// directly after the sets route, and the Themes dictionary next to Sets
        _app.MapGet("/api/v3/lego/themes/{themeId:int}/", (int themeId) =>
        {
            Requests++;

            return Themes.TryGetValue(themeId, out object? payload)
                ? Results.Json(payload)
                : Results.NotFound();
        });
    }

    public Dictionary<string, object> Sets { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<int, object> Themes { get; } = new();

    /// <summary>
    /// One stub serves every test in the database collection, so each test has to start from the
    /// same empty desk. Called by <see cref="DatabaseTest"/>, beside the database reset.
    /// </summary>
    public void Reset()
    {
        Sets.Clear();
        Themes.Clear();
        Requests = 0;
        LastAuthorization = null;
        _failuresRemaining = 0;
    }
```

The themes route deliberately does **not** honour `FailNextRequests`. Retry is already proved on the
sets route in episode 25, and a second proof of the same pipeline is a test that can only ever fail
for reasons unrelated to what it claims to be about.

Now the fixture. `CatalogDatabase` starts the stub before it builds the factory, and hands the
factory the address:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs — as it is
    public CatalogApiFactory Api { get; private set; } = null!;
    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs — replace those lines
    public CatalogApiFactory Api { get; private set; } = null!;

    /// <summary>
    /// Rebrickable, for every endpoint test in this collection. One stub rather than one per test:
    /// the API's configuration is fixed when the factory is built, and an address that does not
    /// exist yet cannot be configured.
    /// </summary>
    public RebrickableStub Rebrickable { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Rebrickable = await RebrickableStub.StartAsync();
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs — the bottom of the file, as it is
    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await _postgres.DisposeAsync();
    }
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs — replace it
    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Rebrickable.DisposeAsync();
        await _postgres.DisposeAsync();
    }
```

`InitializeAsync` also needs the factory line to carry the address — it is the last statement in the
method:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs — as it is, end of InitializeAsync
        Api = new CatalogApiFactory(ConnectionString);
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs — replace it
        Api = new CatalogApiFactory(ConnectionString, Rebrickable.BaseAddress);
```

The factory takes the second argument and overrides one more configuration key. Note what does
*not* change: no service is replaced, no handler is faked, no interface is swapped. **Configuration
is still the only difference between this host and the deployed one** — episode 25's rule, applied
one level up.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogApiFactory.cs — replace the whole class body
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
}
```

And every database test resets both:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/DatabaseTest.cs — as it is
    // Reset before, not after. See step 5.
    public Task InitializeAsync() => Database.ResetAsync();
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/DatabaseTest.cs — replace it
    // Reset before, not after — episode 17, step 5. The stub is shared state for exactly the same
    // reason the database is, so it is emptied in exactly the same place.
    public Task InitializeAsync()
    {
        Database.Rebrickable.Reset();

        return Database.ResetAsync();
    }
```

**Say the uncomfortable half.** A shared stub is shared mutable state across tests that run in the
same collection, and the reason that is tolerable is the same reason the shared Postgres container
is tolerable: the collection runs serially and the fixture is emptied before each test. The moment
someone runs these in parallel, both go wrong together. `RebrickableClientTests` stays as it is —
its own stub, no collection, fully parallel — and the contrast between the two files is the lesson.

---

## Step 4 — Red, then green: a theme has a name

Rebrickable does not return a theme. It returns a `theme_id`:

```json
{ "set_num": "10294-1", "name": "Titanic", "year": 2021, "theme_id": 252, "num_parts": 9092 }
```

`CatalogSet.Theme` is a non-empty string, and episode 34 filters the public catalog on it. `252` is
not a theme anybody wants to filter by, so somebody has to turn the id into "Icons". There are three
candidates and only one of them is honest: ship a hard-coded id→name table in C# (rots silently,
and nobody will ever notice the day Rebrickable renames a theme), store the number and pretend
(a field everyone can see is wrong), or **ask Rebrickable**, which is the only participant that
knows.

So: a second call. Red first, in the client's own test file, which already knows how to start a stub.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Rebrickable/RebrickableClientTests.cs
// after A_transient_failure_is_retried_rather_than_surfaced
    [Fact]
    public async Task A_theme_id_resolves_to_the_name_staff_will_see()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();
        rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)null };

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        RebrickableTheme? theme = await catalog.FindThemeAsync(252, CancellationToken.None);

        Assert.NotNull(theme);
        Assert.Equal("Icons", theme.Name);
    }
```

Red: `RebrickableTheme` and `FindThemeAsync` do not exist. Green is three small files and one method.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableTheme.cs — new file
namespace BrickShare.Catalog.Api.Rebrickable;

public sealed record RebrickableTheme(int Id, string Name);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableThemePayload.cs — new file
using System.Text.Json.Serialization;

namespace BrickShare.Catalog.Api.Rebrickable;

internal sealed record RebrickableThemePayload(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parent_id")]
    int? ParentId);
```

Two types for one concept, exactly as episode 25 argued for sets: the payload is Rebrickable's
shape and may change under us, the record is ours. `ParentId` is read and not yet used — Rebrickable
nests themes, so "Icons" may be the parent of the theme a set actually belongs to. Deliberately not
resolved here; it is part of what **episode 33** takes on when BrickShare grows theme ids of its own.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Rebrickable/IRebrickableCatalog.cs — add below FindSetAsync
    Task<RebrickableTheme?> FindThemeAsync(int themeId, CancellationToken cancellationToken);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableClient.cs — add below FindSetAsync
    public async Task<RebrickableTheme?> FindThemeAsync(
        int themeId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(
            $"lego/themes/{themeId}/", cancellationToken);

        // Null here does not mean the same thing as null from FindSetAsync. A set 404 is a staff
        // typo; a theme 404 is Rebrickable contradicting itself, because the id came from
        // Rebrickable one call ago. The handler treats the two differently. See step 6.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        RebrickableThemePayload payload =
            await response.Content.ReadFromJsonAsync<RebrickableThemePayload>(cancellationToken)
            ?? throw new InvalidOperationException($"Rebrickable sent an empty body for theme {themeId}.");

        return new RebrickableTheme(payload.Id, payload.Name);
    }
```

Green. **And notice what this method did not have to arrange**: no base address, no `key` header, no
timeout, no retry, no circuit breaker. It is the same typed client, so it inherits the whole
resilience pipeline episode 25 registered. That is the payoff for having registered it on the client
rather than around a call site — the second endpoint is free, and so is the twentieth.

---

## Step 5 — The snapshot, and where it is not allowed to live

Persistence mapping and a generated migration: not test-driven, and the script says so. The tests
that matter are the endpoint tests in step 6, which fail loudly if any of this is wrong.

The interesting decision is the namespace, not the code. This type does **not** go in
`BrickShare.Catalog.Domain`.

The domain does not know that Rebrickable exists. `CatalogSet` is a set the shop rents; it would be
exactly the same class if the facts were typed by hand, bought from LEGO, or scraped off a box. A
snapshot is *a record of what a third party said at a particular moment* — infrastructure, and if it
were a domain type then swapping Rebrickable for another source would be a change to the business
model instead of a change to an adapter.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableSnapshot.cs — new file
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Rebrickable;

/// <summary>
/// What Rebrickable said about one set, at one moment, stored so that creating the set does not
/// have to trust the client for any of it. Immutable by construction: a snapshot that could be
/// edited would be a snapshot of nothing in particular.
/// </summary>
public sealed class RebrickableSnapshot
{
    // EF materialises through this. Everything else goes through Capture.
    private RebrickableSnapshot()
    {
    }

    public Guid Id { get; private set; }

    public SetNumber Number { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public int ThemeId { get; private set; }

    public string ThemeName { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public int PieceCount { get; private set; }

    public Uri? ImageUrl { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    public static RebrickableSnapshot Capture(
        RebrickableSet set, RebrickableTheme theme, DateTimeOffset fetchedAt) => new()
    {
        Id = Guid.CreateVersion7(),
        Number = set.Number,
        Name = set.Name,
        ThemeId = theme.Id,
        ThemeName = theme.Name,
        Year = set.Year,
        PieceCount = set.PieceCount,
        ImageUrl = set.ImageUrl,
        FetchedAt = fetchedAt
    };
}
```

`Guid.CreateVersion7()` for the same reason episodes 13 and 20 used it: a sequential id keeps the
index from fragmenting, and this table grows one row per lookup forever.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/RebrickableSnapshotConfiguration.cs — new file
using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class RebrickableSnapshotConfiguration : IEntityTypeConfiguration<RebrickableSnapshot>
{
    public void Configure(EntityTypeBuilder<RebrickableSnapshot> builder)
    {
        builder.ToTable("rebrickable_snapshots");

        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).HasColumnName("id");

        builder.Property(snapshot => snapshot.Number)
            .HasColumnName("set_number")
            .HasConversion(number => number.Value, value => SetNumber.Parse(value))
            .HasMaxLength(SetNumber.MaxLength)
            .IsRequired();

        // No unique index, and this is the one line in the file worth arguing about. Looking the
        // same set up twice is normal — two staff members, or one who closed the tab — and each
        // lookup is a separate fact with its own fetched_at. The invariant "one catalog_set per
        // set number" is already enforced one table over, by episode 24's index.
        builder.HasIndex(snapshot => snapshot.Number)
            .HasDatabaseName("ix_rebrickable_snapshots_set_number");

        builder.Property(snapshot => snapshot.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(snapshot => snapshot.ThemeId).HasColumnName("theme_id").IsRequired();

        builder.Property(snapshot => snapshot.ThemeName)
            .HasColumnName("theme_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(snapshot => snapshot.Year).HasColumnName("year").IsRequired();
        builder.Property(snapshot => snapshot.PieceCount).HasColumnName("piece_count").IsRequired();

        // EF would convert a Uri by convention. Naming the converter makes the column shape a
        // decision rather than a default somebody has to go and look up.
        builder.Property(snapshot => snapshot.ImageUrl)
            .HasColumnName("image_url")
            .HasConversion(new UriToStringConverter())
            .HasMaxLength(2048);

        builder.Property(snapshot => snapshot.FetchedAt)
            .HasColumnName("fetched_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
```

The `name` and `theme_name` lengths match `CatalogSetConfiguration` deliberately: a snapshot that
could hold a name too long to copy into `catalog_sets` is a failure deferred to episode 28's create
call, where it would arrive as a `DbUpdateException` nobody expected.

**No `xmin` row version here**, unlike every other table in this model. A snapshot is written once
and never updated, so there is no second writer to lose to.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — as it is
    public DbSet<Copy> Copies => Set<Copy>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — add below it
    public DbSet<RebrickableSnapshot> Snapshots => Set<RebrickableSnapshot>();
```

`ApplyConfigurationsFromAssembly` finds the new configuration with no further wiring — episode 16
paid for that once.

```bash
cd src/Catalog/BrickShare.Catalog.Api
dotnet ef migrations add AddRebrickableSnapshots
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Migrations/…_AddRebrickableSnapshots.cs — generated, not typed
            migrationBuilder.CreateTable(
                name: "rebrickable_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    theme_id = table.Column<int>(type: "integer", nullable: false),
                    theme_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    piece_count = table.Column<int>(type: "integer", nullable: false),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_rebrickable_snapshots", x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_rebrickable_snapshots_set_number",
                table: "rebrickable_snapshots",
                column: "set_number");
```

Read it, do not trust it: this is a new table and nothing else. **A migration that touches
`catalog_sets` in this episode would be a bug**, and the pipeline's gated migration job from
episode 19 is the last place you want to discover that.

---

## Step 6 — Green: the endpoint

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/LookupRequestValidator.cs — new file
using BrickShare.Catalog.Domain;

using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class LookupRequestValidator : AbstractValidator<LookupRequest>
{
    public LookupRequestValidator()
    {
        // Delegated, exactly as episode 22 delegated it: the domain owns what a set number is.
        // This rule also keeps a malformed number from costing a Rebrickable request.
        RuleFor(request => request.SetNumber)
            .Must(value => SetNumber.TryParse(value, out _))
            .WithMessage($"A set number is required, and cannot be longer than {SetNumber.MaxLength} characters.");
    }
}
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — new file
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http.HttpResults;

namespace BrickShare.Catalog.Api.Endpoints;

public static class CatalogLookupEndpoints
{
    public static RouteGroupBuilder MapCatalogLookups(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog/lookups")
            .WithTags("Catalog lookups");

        group.MapPost("/", LookUpAsync);

        return group;
    }

    private static async Task<Results<Created<LookupResponse>, ValidationProblem, ProblemHttpResult>> LookUpAsync(
        LookupRequest request,
        IValidator<LookupRequest> validator,
        IRebrickableCatalog rebrickable,
        CatalogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        SetNumber number = SetNumber.Parse(request.SetNumber);

        RebrickableSet? set = await rebrickable.FindSetAsync(number, cancellationToken);
        if (set is null)
        {
            // The likeliest thing that happens at this endpoint is a typo, and a typo is a 404.
            // See below for why this is not a 502 and not a 422.
            return TypedResults.Problem(
                title: "Set not found",
                detail: $"Rebrickable has no set numbered {number}. Set numbers on the box usually end in -1.",
                statusCode: StatusCodes.Status404NotFound);
        }

        RebrickableTheme? theme = await rebrickable.FindThemeAsync(set.ThemeId, cancellationToken);
        if (theme is null)
        {
            // A different failure entirely: the id came from Rebrickable one call ago, so this is
            // Rebrickable disagreeing with itself. Nobody here can fix it by retyping anything,
            // and a draft with no theme cannot be catalogued, so the lookup fails as an upstream
            // problem rather than pretending the set does not exist.
            return TypedResults.Problem(
                title: "Set facts incomplete",
                detail: $"Rebrickable knows set {number} but not its theme ({set.ThemeId}), so the draft would be incomplete.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        RebrickableSnapshot snapshot = RebrickableSnapshot.Capture(set, theme, clock.GetUtcNow());

        database.Snapshots.Add(snapshot);
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/catalog/lookups/{snapshot.Id}", LookupResponse.From(snapshot));
    }
}

/// <summary>
/// The only thing a client is entitled to send at this endpoint: which set to ask about.
/// </summary>
public sealed record LookupRequest(string SetNumber);

/// <summary>
/// The prefilled draft. Every field but the id came from Rebrickable, which is the whole point:
/// episode 28's create call quotes the id and cannot contradict the rest.
/// </summary>
public sealed record LookupResponse(
    Guid LookupId,
    string SetNumber,
    string Name,
    string Theme,
    int Year,
    int PieceCount,
    Uri? ImageUrl,
    DateTimeOffset FetchedAt)
{
    public static LookupResponse From(RebrickableSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.Number.Value,
        snapshot.Name,
        snapshot.ThemeName,
        snapshot.Year,
        snapshot.PieceCount,
        snapshot.ImageUrl,
        snapshot.FetchedAt);
}
```

Three lines of wiring:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — as it is
builder.Services.AddScoped<IValidator<CatalogueSetRequest>, CatalogueSetRequestValidator>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — add below it
builder.Services.AddScoped<IValidator<LookupRequest>, LookupRequestValidator>();

// The clock as a dependency rather than a static call. Nothing fakes it yet; episode 28 decides
// whether a snapshot goes stale, and that decision is untestable if `fetched_at` comes from
// DateTimeOffset.UtcNow inside a handler.
builder.Services.AddSingleton(TimeProvider.System);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — as it is, near the bottom
v1.MapCatalogSets();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — add below it
v1.MapCatalogLookups();
```

Run the suite: step 2's test is green.

### The rest of the suite — rules, not design

Three more tests, and they pass the moment they are written. `CLAUDE.md` is explicit that this is
fine and should be said rather than hidden: **some tests drive a design and some describe a rule.**
The first one drove this endpoint's shape. These three pin down behaviour that would otherwise be
free to drift.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/LookupTests.cs
// after A_lookup_returns_the_facts_staff_do_not_type
    [Fact]
    public async Task A_set_Rebrickable_has_never_heard_of_is_not_found_rather_than_broken()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "99999-9" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_malformed_set_number_never_reaches_Rebrickable()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, Database.Rebrickable.Requests);
    }

    [Fact]
    public async Task Looking_the_same_set_up_twice_is_two_snapshots()
    {
        Database.Rebrickable.Sets["10294-1"] = Titanic();
        Database.Rebrickable.Themes[252] = Icons();

        HttpClient client = Database.Api.CreateClient();

        await client.PostAsJsonAsync("/api/v1/catalog/lookups", new { setNumber = "10294-1" });
        await client.PostAsJsonAsync("/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(2, await dbContext.Snapshots.CountAsync());
    }
```

Two `using` directives for the last one:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/LookupTests.cs — add to the top
using BrickShare.Catalog.Api.Persistence;

using Microsoft.EntityFrameworkCore;
```

`A_malformed_set_number_never_reaches_Rebrickable` is the one to point at. Its interesting assertion
is not the `400` — it is `Requests == 0`. Validation at the edge is usually sold as a nicer error
message; here it is also **a rate-limited third-party quota that an empty string cannot spend.**

### The close calls, stated

**`404`, not `502`, and not `422`.** All three are defensible and the difference is *whose fault it
is*. A `502` says Rebrickable is broken, which is a lie that sends somebody to check a status page
when the actual problem is a mistyped digit — and worse, it is the status the *real* outage needs,
so overloading it makes the two indistinguishable in a dashboard. `422` says the request was
well-formed but unprocessable, which is closer, and it is the honest runner-up: nothing about
`{"setNumber": "99999-9"}` is malformed. `404` wins because the resource the client asked about is
the *set*, and that set does not exist. It also matches what the same person would get from
Rebrickable directly. The same reasoning that picked `409` in episode 23: **the status code is a
claim about who has to do something next.**

**No expiry, and no single-use flag.** A snapshot could be an hour old or a month old when create
quotes it, and Rebrickable could have corrected a piece count in between. Two ways to handle it: an
expiry check when create reads the snapshot, or nothing. Nothing ships, because the failure mode is
a set catalogued with a slightly stale piece count — a value that can be corrected — and the cost of
an expiry is a staff member losing a half-filled form to a rule nobody explained. It is genuinely
close, it belongs to **episode 28** where create reads the row, and `fetched_at` is stored so the
decision stays available. That is also why the clock is injected rather than called statically.

---

## Step 7 — The `.http` file

`BrickShare.Catalog.Api.http` still contains nothing but the health checks — the catalog requests
episodes 21 to 24 talked about never made it into the file. Fix that here, because the next four
episodes all start by calling this endpoint.

```http
### BrickShare.Catalog.Api.http — append

### Catalogue a set the way episode 20 left it. The facts are client-supplied; episode 28 ends that.
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{ "setNumber": "10294-1", "name": "Titanic", "theme": "Icons", "year": 2021,
  "pieceCount": 9092, "retailPrice": 629.99, "baseRentalPrice": 60.00,
  "minimumRentalDays": 7, "minimumAge": 18 }

### Look a set up. 201, and the facts come back without anybody typing them.
POST {{host}}/api/v1/catalog/lookups
Content-Type: application/json

{ "setNumber": "10294-1" }

### A typo. 404 with a problem document, not a 502 — this is the most likely failure here.
POST {{host}}/api/v1/catalog/lookups
Content-Type: application/json

{ "setNumber": "99999-9" }

### Nonsense. 400 from the validator, and Rebrickable is never called.
POST {{host}}/api/v1/catalog/lookups
Content-Type: application/json

{ "setNumber": "" }
```

Run the first two on camera against a real key. The second returns a Titanic nobody typed — which is
the whole episode in one response body.

---

## What this episode is not

**Not the fix.** `POST /catalog/sets` still accepts nine fields and step 1's fake Titanic still
gets a `201`. Shipping the lookup first is the only order available — create cannot require a
`lookupId` that no endpoint issues — but it does mean the vulnerability is open at the end of this
episode. **Say that at the end rather than letting the split hide it.**

**No image copied into Blob.** `docs/architecture/catalog.md` wants the set image pulled into our
own storage during the lookup, while staff are waiting on a network call anyway, so that BrickShare
stops depending on a third party's CDN. Right, and it needs a storage account, a container, a
client and a failure policy — that is **episode 36**. The snapshot stores the URL until then.

**No theme entity.** `theme_name` is text, copied from Rebrickable, and episode 34 will filter the
public catalog on it. Free text is a bad thing to filter on, and **episode 33** replaces it with a
`themes` table, a foreign key and a migration that backfills from the column this episode writes.
The nested-theme question — `parent_id` — belongs to that episode too.

**No caching.** Every lookup of the same set number is a fresh pair of Rebrickable calls. Cataloguing
a set is a rare, human-paced operation, and a cache here would be a second source of truth for the
facts this episode exists to make single-sourced.

**No authorization.** "Anyone holding a staff token" is still literally anyone: these endpoints are
open until **episode 38**. The design work has to happen first regardless — an authenticated caller
who can still invent a product is an authenticated attacker.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green, including the four `RebrickableClientTests` |
| `dotnet ef migrations list` | `AddRebrickableSnapshots` last, and nothing else new |
| `\d rebrickable_snapshots` in psql | Nine columns, one non-unique index on `set_number` |
| The `.http` lookup on `10294-1`, twice | `201` twice, two different `lookupId` values |
| `select count(*) from rebrickable_snapshots` | `2` |
| The `.http` lookup on `99999-9` | `404`, `application/problem+json`, a `traceId` extension |
| The `.http` lookup on `""` | `400`, and nothing new in the table |
| `git diff --stat src/Catalog/BrickShare.Catalog.Domain` | Empty — the domain never learned what Rebrickable is |

That last row is the one to actually run. The point of step 5's argument is only proved by the
absence of a change, and an absence is not visible on screen unless you go and show it.

## Next

[Episode 28 — The request body that loses its facts](episode-28.md):
create is rewritten to take a `lookupId` and the four fields staff are entitled to decide — retail
price, base rental price, minimum rental duration, age rating. Five fields leave the request record,
the validator shrinks, and step 1's fake Titanic stops being possible.

The general form of the question is the thing to carry out of both episodes: **whose data is this?**,
asked of every field in every request body. A field the client should not be able to choose must not
be a field the client can send — and it costs one extra endpoint.
