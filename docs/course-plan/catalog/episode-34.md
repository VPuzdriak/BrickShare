# Episode 34 — Browse and filter

← [Course plan](catalog-api.md) · Previous: [Episode 33 — Themes of our own](episode-33.md)

Episode 33 ended on a `psql` query and one sentence:

> **That result set is episode 34's filter list**, produced by a query with no `distinct` in it, over a
> column nothing can misspell.

This is that episode, and it is the first one where the **customer** is the caller. Every endpoint
so far has been staff typing at a counter: look a set up, catalogue it, register a box, retire a
box. UC-7 is somebody at home deciding what to build this weekend, and it asks for two things the
write side never needed:

> **UC-7.2 — Search and filter.** By name or set number, and by the attributes that matter when
> choosing something to build: theme, piece count, age rating, price, and whether it is available
> right now. Each result shows the set, **how many copies are available**, and a **starting
> price** — the cheapest copy anyone could reserve right now.

This episode builds all of that except the words *by name or set number*. Search is
[episode 36](catalog-api.md#episode-36--search-and-page-two), because `pg_trgm` and paging are an episode of their own.

"Starting price" is the phrase that makes this episode more than a `Where` clause. A price is not a
column. It is `base_rental_price × multiplier[grade]`, and the multipliers have lived in a
unit-test helper since episode 12 with nowhere in the database to go. So the episode also has to
give them a table, and it has to decide where a formula that already exists in C# gets computed when
the question comes from a query.

**Done when** a customer can list the catalog filtered by theme, piece count, age, price and
availability, every result shows its available count and starting price, and a set with no copies
still shows, with `startingPrice: null` rather than a zero.

> **Runtime: about 17 minutes — over the 10–15 budget, and said so up front.** Step 8 (the indexes
> we do *not* add) stays in full: it is the step that answers "shouldn't this be indexed?", which
> every student will ask. If time has to come out, it comes from **step 7**: show one row of the
> validation theory and the validator, and skip the other three rows. That saves about a minute.
>
> If it still runs long, there is a clean seam after step 5, marked in place:
>
> - **34a — "Browse" (steps 1–5, ~11 minutes).** The themes list, the price table, the view and the
>   starting price. Ends with every test green and a listing a customer could use.
> - **34b — "Filter" (steps 6–9, ~6 minutes).** The filters, their validation, the indexes, the
>   OpenAPI metadata and the `.http` requests.
>
> The second half is short on its own, which is why recording it as one is the recommendation.

## Before recording

- Episode 33 merged: `catalog_sets.theme_id` is a foreign key, `themes` has rows, the whole suite
  is green.
- `docker compose up` for Postgres, `dotnet user-secrets` still holding your Rebrickable key.
- A branch. **There is a migration this episode**, and part of it is written by hand.
- A database with a few sets in it for steps 8 and 9 — the ones episode 33 catalogued are enough.
  Register a copy or two against one of them through the `.http` file, so that one set has a
  starting price and another does not.
- `docs/IDEA.md` open at UC-7 and its business rules; `docs/architecture/catalog.md` open at
  *"Derived values are computed, never stored"* and at *"Search — and why nothing extra is
  needed"*. Both are quoted on screen.
- `tests/BrickShare.Catalog.UnitTests/Pricing/Multipliers.cs` open. The table in it is about to
  become four rows in Postgres.

**Three of the nine steps are not driven by a test, and each says so where it appears**: step 3 is
configuration and a migration, step 8 is an argument with a `psql` prompt in it, and step 9 is
metadata and a request collection. **One red in step 5 is not a TDD red** — it is the test harness
disagreeing with the migration — and it says so too.

Every sample below names its file and where in it the code goes. Where something is edited in a file
that already exists, the **first block is what is already there** — the anchor to find on screen —
and the **second block is what to paste**. Every block is copy-paste clean: no markers, no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/IntegrationTests/CatalogFlow.cs` | Two stocked sets, a catalogue helper, a copy helper |
| `tests/…/IntegrationTests/Browse/ThemeListTests.cs` | **New** — one test |
| `tests/…/IntegrationTests/Browse/BrowseSetsTests.cs` | **New** — five tests, two of them theories |
| `tests/…/IntegrationTests/CatalogDatabase.cs` | One table added to Respawn's ignore list |
| `src/…/Api/Endpoints/BrowseEndpoints.cs` | **New** — the public route group, two handlers |
| `src/…/Api/Endpoints/BrowseQuery.cs` | **New** — the filter record and its validator |
| `src/…/Api/Persistence/GradeMultiplier.cs` | **New** — a row, and its configuration |
| `src/…/Api/Persistence/CatalogSetListing.cs` | **New** — a read model, and its configuration |
| `src/…/Api/Persistence/CatalogDbContext.cs` | Two `DbSet`s |
| `src/…/Api/Migrations/…_AddCatalogBrowse.cs` | **Generated, then extended by hand** |
| `src/…/Api/Program.cs` | One line, one comment, one description |
| `src/…/Api/BrickShare.Catalog.Api.http` | Browse requests appended |

Nothing in `Domain/`. **The domain already knows how to price a copy; this episode is about asking
that question a few thousand times at once.**

---

## Step 1 — The filter list: `GET /catalog/themes`

A filter needs something to filter by. Before a customer can say *"only Icons"*, the client has to
know which themes exist — and episode 33 said exactly which ones those are:

> A theme with no sets shows up in exactly one place — episode 34's filter list — and that query has
> to exclude empty themes anyway, because a filter option that returns nothing is a bug regardless
> of how the row got there.

So that is the test. First, test data. Every integration test so far has catalogued the Titanic,
and a filter over one set filters nothing. Two more sets, with different themes, sizes, ages and
prices, go into the shared flow file.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogFlow.cs — the anchor, already there
    public static async Task<Guid> CatalogueTitanicAsync(this CatalogDatabase database, HttpClient client)
    {
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogFlow.cs — paste directly above CatalogueTitanicAsync
    /// <summary>
    /// Any set, through the front door: Rebrickable stubbed, looked up, catalogued. The Titanic keeps
    /// its own helper because thirty tests already call it by name.
    /// </summary>
    public static async Task<Guid> CatalogueAsync(
        this CatalogDatabase database, HttpClient client, StockedSet set)
    {
        database.Rebrickable.Sets[set.SetNumber] = new
        {
            set_num = set.SetNumber,
            name = set.Name,
            year = set.Year,
            theme_id = set.ThemeId,
            num_parts = set.PieceCount,
            set_img_url = $"https://cdn.rebrickable.com/media/sets/{set.SetNumber}.jpg"
        };

        database.Rebrickable.Themes[set.ThemeId] =
            new { id = set.ThemeId, name = set.ThemeName, parent_id = (int?)null };

        HttpResponseMessage lookup = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = set.SetNumber });

        lookup.EnsureSuccessStatusCode();

        LookupResponse? draft = await lookup.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets",
            new
            {
                lookupId = draft.LookupId,
                retailPrice = set.RetailPrice,
                baseRentalPrice = set.BaseRentalPrice,
                minimumRentalDays = 7,
                minimumAge = set.MinimumAge
            });

        response.EnsureSuccessStatusCode();

        CatalogSetResponse? created = await response.Content.ReadFromJsonAsync<CatalogSetResponse>();

        Assert.NotNull(created);

        return created.Id;
    }

```

And the record it takes, at the bottom of the same file, outside the class:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogFlow.cs — at the end of the file
/// <summary>
/// A set a test can put on the shelf. Chosen to differ from the Titanic — 9092 pieces, age 18,
/// Icons, 60.00 — on every axis a customer can filter on.
/// </summary>
internal sealed record StockedSet(
    string SetNumber,
    string Name,
    int Year,
    int ThemeId,
    string ThemeName,
    int PieceCount,
    decimal RetailPrice,
    decimal BaseRentalPrice,
    int MinimumAge)
{
    public static StockedSet Concorde { get; } =
        new("10318-1", "Concorde", 2023, 252, "Icons", 2083, 199.99m, 25.00m, 18);

    public static StockedSet MainStreetBuilding { get; } =
        new("41704-1", "Main Street Building", 2022, 494, "Friends", 1682, 129.99m, 15.00m, 8);
}
```

**Caveman version:** one rock not make pile. Filter one set, filter nothing. Put three different
rock on shelf — big, small, for child — then filter mean something.

Now the red.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/ThemeListTests.cs — new file
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.IntegrationTests.Browse;

public class ThemeListTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task The_filter_list_is_every_theme_that_has_a_set_in_it_by_name()
    {
        HttpClient client = Database.Api.CreateClient();

        await Database.CatalogueTitanicAsync(client);
        await Database.CatalogueAsync(client, StockedSet.Concorde);
        await Database.CatalogueAsync(client, StockedSet.MainStreetBuilding);

        // Episode 33's harmless orphan: a theme adopted by a catalogue request that then failed.
        await using (CatalogDbContext dbContext = Database.NewDbContext())
        {
            dbContext.Themes.Add(Theme.Adopt(999, "Aardvarks"));
            await dbContext.SaveChangesAsync();
        }

        ThemesResponse? themes =
            await client.GetFromJsonAsync<ThemesResponse>("/api/v1/catalog/themes", Database.Api.Json);

        Assert.NotNull(themes);
        Assert.Equal(new[] { "Friends", "Icons" }, themes.Themes.Select(theme => theme.Name));
    }
}
```

The one assertion carries three rules. Icons appears **once**, even though two sets belong to it.
Aardvarks does **not** appear, although it is first alphabetically, because there is nothing to
find behind it. And the order is by **name**, because a person is going to read this list.

Red: **it does not compile.** `ThemesResponse` does not exist — `CLAUDE.md` counts that as a red.

**Caveman version:** test ask for list. List not exist, word not exist. Compiler say no. That
the red.

Green is a new file — and a **new route group**, which is the decision worth a paragraph.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — new file
using BrickShare.Catalog.Api.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Endpoints;

/// <summary>
/// What a customer can read. Kept apart from the staff groups on purpose: episode 40 puts
/// RequireAuthorization() on those, and this group is the one it leaves alone.
/// </summary>
public static class BrowseEndpoints
{
    public static RouteGroupBuilder MapBrowse(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog")
            .WithTags("Browse");

        group.MapGet("/themes", ListThemesAsync);

        return group;
    }

    private static async Task<Ok<ThemesResponse>> ListThemesAsync(
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        List<ThemeResponse> themes = await database.Themes
            .Where(theme => database.Sets.Any(set => set.ThemeId == theme.Id))
            .OrderBy(theme => theme.Name)
            .Select(theme => new ThemeResponse(theme.Id, theme.Name))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ThemesResponse(themes));
    }
}

public sealed record ThemesResponse(IReadOnlyList<ThemeResponse> Themes);

public sealed record ThemeResponse(Guid Id, string Name);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — the anchor, already there
v1.MapCatalogSets();
v1.MapCatalogLookups();
v1.MapCopies();
v1.MapCopyRetirements();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — what it becomes
v1.MapCatalogSets();
v1.MapCatalogLookups();
v1.MapCopies();
v1.MapCopyRetirements();
v1.MapBrowse();
```

Green.

**Caveman version:** ask database: which theme have at least one set? Sort by name. Give back
name and id. Empty theme not in list — no point show door with nothing behind.

### Three decisions in twenty lines

**A separate group, although `/catalog/sets` already has one.** Step 2 adds `GET /catalog/sets`,
the same URL as the staff `POST`, and it would be natural to add it to `CatalogSetEndpoints`. It
goes here instead, because **the split that matters in this service is not by resource, it is by
caller.** Episode 40 makes every staff route require a signed-in staff member, and it does that with
one `RequireAuthorization()` per group. If a public `GET` shares a group with a staff `POST`, that
one line becomes per-endpoint bookkeeping, and the day somebody forgets it, a staff route is public.
One group per audience means the security decision is made once, where it is visible.

**`Any` rather than a join and a `distinct`.** The question is *"does at least one set point at
this theme"*, and `Any` is that question in C#. EF translates it to `EXISTS`, which Postgres can
stop evaluating at the first match. A join would produce one row per set and then throw most of them
away with `DISTINCT` — the same answer, with more work, and a query that says something different
from what it means.

**A wrapper object, not a bare array.** `{ "themes": [...] }` rather than `[...]`. Step 2 returns
sets the same way, and episode 36 adds a `next` cursor next to them. A bare JSON array has nowhere
to put a second field. **Adding a field to an object is a compatible change; turning an array into
an object breaks every client that parses it.**

No `AsNoTracking()`: the query projects into a record, and EF only tracks entities. The index
episode 33 put on `themes.name` — *"episode 34 orders the filter list by this column"* — is the one
this `OrderBy` was promised.

---

## Step 2 — Red: a set with no copies still shows

The listing. Its first test is the business rule that a query written from intuition breaks —
UC-7, in `docs/IDEA.md`:

> A set with no available copies still shows in full — otherwise its copies could not be
> subscribed to.

A set with **no copies at all** is the extreme case of that, and IDEA.md allows one — a planned
purchase, or a set whose every box has been retired. An `INNER JOIN` from sets to copies deletes it
from the listing, silently.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — new file
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;

namespace BrickShare.Catalog.IntegrationTests.Browse;

public class BrowseSetsTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_set_with_no_copies_still_shows_in_full()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);

        SetListingResponse titanic = Assert.Single(page.Sets);

        Assert.Equal(setId, titanic.Id);
        Assert.Equal("Titanic", titanic.Name);
        Assert.Equal("Icons", titanic.Theme);
        Assert.Equal(0, titanic.AvailableCount);

        // Not 0.00. A price of zero is a claim that the set is free; there is no price at all.
        Assert.Null(titanic.StartingPrice);
    }
}
```

Red: it does not compile. Add the two response records to `BrowseEndpoints.cs`, below the theme
records, so that it does:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — at the end of the file
public sealed record BrowseSetsResponse(IReadOnlyList<SetListingResponse> Sets);

public sealed record SetListingResponse(
    Guid Id,
    string SetNumber,
    string Name,
    string Theme,
    int Year,
    int PieceCount,
    int MinimumAge,
    int MinimumRentalDays,
    int AvailableCount,
    decimal? StartingPrice);
```

Run it again: **`405 Method Not Allowed`**. That is a better red than a `404` and worth ten seconds.
The URL exists — the staff `POST` lives there — so ASP.NET Core knows the path and says the verb is
wrong. The test found the exact thing that is missing: a `GET` on a resource that so far only
accepted writes.

**Caveman version:** knock on door with GET. Door exist — but door only open for POST. Door say
"wrong knock". Good red: say exactly what missing.

`StartingPrice` is `decimal?` on purpose. The architecture document is blunt about this — *"it
simply has no starting price, and the response says so rather than inventing a zero"* — and the
nullable type is how the OpenAPI document says it too.

---

## Step 3 — The price table

**Not driven by a test.** This is configuration and a table — `CLAUDE.md`'s infrastructure
exemption. The test in step 5 is what proves it is right.

The starting price is `base_rental_price × multiplier[grade]` for the cheapest available copy. The
base price is in `catalog_sets`, the grade is in `copies`, and the multiplier is in… a unit-test
helper.

```csharp
// tests/BrickShare.Catalog.UnitTests/Pricing/Multipliers.cs — already there, do not change it
        return new GradeMultipliers(new Dictionary<ConditionGrade, decimal>
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.85m,
            [ConditionGrade.Good] = 0.70m,
            [ConditionGrade.Fair] = 0.55m
        });
```

Nothing has needed the real numbers until now: every price so far was computed in a unit test with
the table passed in. A listing needs them in the database, because the database is where a few
thousand copies get compared. So the four numbers become four rows — the `grade_multipliers` table
the architecture document has listed since before episode 1.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/GradeMultiplier.cs — new file
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

/// <summary>
/// One row of the shop's pricing table. The domain's <see cref="Domain.Pricing.GradeMultipliers"/>
/// is the rule; this is where its numbers are kept.
/// </summary>
public sealed record GradeMultiplier(ConditionGrade Grade, decimal Multiplier);

public sealed class GradeMultiplierConfiguration : IEntityTypeConfiguration<GradeMultiplier>
{
    public void Configure(EntityTypeBuilder<GradeMultiplier> builder)
    {
        // A zero here would make every copy of that grade free. GradeMultipliers refuses one in C#;
        // episode 40's admin edit writes to this table, and the database refuses one too.
        builder.ToTable("grade_multipliers", table =>
            table.HasCheckConstraint("ck_grade_multipliers_positive", "multiplier > 0"));

        builder.HasKey(row => row.Grade);

        // Stored as text, spelled exactly as copies.grade is, so the two join without a lookup.
        builder.Property(row => row.Grade)
            .HasColumnName("grade")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(row => row.Multiplier)
            .HasColumnName("multiplier")
            .HasPrecision(4, 2)
            .IsRequired();

        // The standard table the pricing tests have assumed since episode 12. Placeholders until
        // an admin edits them in episode 40 — but a shop with no prices cannot open.
        builder.HasData(
            new GradeMultiplier(ConditionGrade.New, 1.00m),
            new GradeMultiplier(ConditionGrade.Excellent, 0.85m),
            new GradeMultiplier(ConditionGrade.Good, 0.70m),
            new GradeMultiplier(ConditionGrade.Fair, 0.55m));
    }
}
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — the anchor, already there
    public DbSet<Copy> Copies => Set<Copy>();
    public DbSet<RebrickableSnapshot> Snapshots => Set<RebrickableSnapshot>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — what it becomes
    public DbSet<Copy> Copies => Set<Copy>();
    public DbSet<RebrickableSnapshot> Snapshots => Set<RebrickableSnapshot>();
    public DbSet<GradeMultiplier> GradeMultipliers => Set<GradeMultiplier>();
```

No migration yet. The next step adds the view, and **one migration should hold one change that
makes sense on its own**. A price table with nothing reading it is not that change; a table and the
view that reads it are.

**Caveman version:** price rule live in test. Test not database. Database need number to compare
thousand box. So number move into table — four row, one per grade. Table say "no zero", because
zero mean box free.

### Why the row lives in `Persistence/`, not `Domain/`

`Domain/Pricing/GradeMultipliers` already exists, and it is the right shape for the domain: the
whole table as one value, refusing a missing grade or a non-positive multiplier in its constructor.
`GradeMultiplier` — singular — is not a domain concept. It is **how the four numbers sit in
Postgres**, one per row because that is how a relational table holds a map. Putting it next to
`Copy` and `CatalogSet` would suggest the domain thinks about multipliers one at a time, and it does
not.

**Why `HasData` and not an admin endpoint first.** A shop cannot price anything until the
multipliers exist, and the endpoint that edits them is episode 40's, Admin-only and two-phase.
Seeding the standard table makes the service usable today and gives episode 40 something to edit.
The honest cost: `HasData` values live in a migration forever, so episode 40 has to treat the seed
as *initial* data and never re-seed it. That is said again there.

---

## Step 4 — Green: a read model that is a view

Now the listing itself. The obvious version is LINQ over `database.Sets` with a sub-query for the
copies — and it does not work, for a reason worth showing rather than asserting:

```csharp
// Do not paste. This is the query that does not translate.
set.BaseRentalPrice.Amount * multiplier
```

`BaseRentalPrice` is a `Money`, stored through `MoneyConverter`. EF can compare a converted value
with another of the same type, but it cannot see **inside** one: `.Amount` has no SQL translation,
and `Money * decimal` is a C# operator EF has never heard of. The query throws at runtime with
*"could not be translated"*.

**Caveman version:** money in special box. Database see box, not see inside box. Cannot do sum
with thing it cannot see.

There are three ways out, and this is a genuine judgement call:

| | Computed in C# after loading | Raw SQL per request (`FromSql`) | **A view, mapped as a keyless entity** (chosen) |
| --- | --- | --- | --- |
| Filters on price and availability | Load every set and every copy, filter in memory | Hand-built `WHERE` per filter combination | LINQ `Where` over plain columns, composed |
| Where the formula lives | `PriceCalculator` — once | A SQL string in an endpoint | A SQL view in a migration |
| Cost of a schema change | None | Find every string | Postgres refuses to alter a column the view reads until the view is dropped |

The view wins because **the filters in step 6 have to compose**: price, availability, theme and
age in any combination. Over a view, each one is a `Where` on a column, and EF writes the SQL. The
aggregate is written **once**, in SQL, where it is cheapest to compute.

What it costs, stated plainly: **the pricing formula now exists twice**, in `PriceCalculator` and in
the view. The step-5 test pins them together, and is the reason that test exists.

**Caveman version:** make database keep a ready-made answer shape: "set, how many box free,
cheapest price". Then filter on that shape like normal table. Cost: price rule now in two place.
Test watch both.

The read model:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogSetListing.cs — new file
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

/// <summary>
/// One row of the public catalog: a set, and the two numbers a customer browses by that no
/// table stores. Read-only, keyless, and computed by Postgres on every query — see the view in
/// the AddCatalogBrowse migration.
/// </summary>
public sealed class CatalogSetListing
{
    public required Guid Id { get; init; }

    public required string SetNumber { get; init; }

    public required string Name { get; init; }

    public required Guid ThemeId { get; init; }

    public required string ThemeName { get; init; }

    public required int Year { get; init; }

    public required int PieceCount { get; init; }

    public required int MinimumAge { get; init; }

    public required int MinimumRentalDays { get; init; }

    public required int AvailableCount { get; init; }

    /// <summary>Null when no copy is available. Never zero for "no price".</summary>
    public decimal? StartingPrice { get; init; }
}

public sealed class CatalogSetListingConfiguration : IEntityTypeConfiguration<CatalogSetListing>
{
    public void Configure(EntityTypeBuilder<CatalogSetListing> builder)
    {
        // A view, not a table: EF reads from it and never generates a CreateTable for it. The
        // migration owns its SQL.
        builder.ToView("catalog_set_listings");
        builder.HasNoKey();

        builder.Property(listing => listing.Id).HasColumnName("id");
        builder.Property(listing => listing.SetNumber).HasColumnName("set_number");
        builder.Property(listing => listing.Name).HasColumnName("name");
        builder.Property(listing => listing.ThemeId).HasColumnName("theme_id");
        builder.Property(listing => listing.ThemeName).HasColumnName("theme_name");
        builder.Property(listing => listing.Year).HasColumnName("year");
        builder.Property(listing => listing.PieceCount).HasColumnName("piece_count");
        builder.Property(listing => listing.MinimumAge).HasColumnName("minimum_age");
        builder.Property(listing => listing.MinimumRentalDays).HasColumnName("minimum_rental_days");
        builder.Property(listing => listing.AvailableCount).HasColumnName("available_count");

        // Plain decimal, not Money: this is a number to filter and sort on, not a value the
        // domain will do arithmetic with. The column type matches every other price column.
        builder.Property(listing => listing.StartingPrice)
            .HasColumnName("starting_price")
            .HasColumnType("numeric(10,2)");
    }
}
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — the anchor, from step 3
    public DbSet<GradeMultiplier> GradeMultipliers => Set<GradeMultiplier>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — what it becomes
    public DbSet<GradeMultiplier> GradeMultipliers => Set<GradeMultiplier>();
    public DbSet<CatalogSetListing> Listings => Set<CatalogSetListing>();
```

### The migration

**Not driven by a test — infrastructure.** Generate it:

```bash
dotnet ef migrations add AddCatalogBrowse \
  --project src/Catalog/BrickShare.Catalog.Api
```

Read what came out. EF generated the `grade_multipliers` table, its check constraint and four
`InsertData` rows — and **nothing for the view**. `ToView` tells EF to *read* from
`catalog_set_listings`, not to create it. The `Up` ends like this:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Migrations/…_AddCatalogBrowse.cs — generated, the anchor at the end of Up
            migrationBuilder.InsertData(
                table: "grade_multipliers",
                columns: new[] { "grade", "multiplier" },
                values: new object[,]
                {
                    { "Excellent", 0.85m },
                    { "Fair", 0.55m },
                    { "Good", 0.70m },
                    { "New", 1.00m }
                });
        }
```

Paste the view after the `InsertData`, as the last statement of `Up`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Migrations/…_AddCatalogBrowse.cs — last statement in Up
            // Hand-written: EF maps the view but never generates one. Every rule in UC-7's list
            // is in here, so read it as a specification, not as plumbing.
            migrationBuilder.Sql(
                """
                create view catalog_set_listings as
                select
                    s.id,
                    s.set_number,
                    s.name,
                    s.theme_id,
                    t.name as theme_name,
                    s.year,
                    s.piece_count,
                    s.minimum_age,
                    s.minimum_rental_days,
                    (count(c.id) filter (where c.status = 'Available'))::int as available_count,
                    round(
                        s.base_rental_price * min(m.multiplier) filter (where c.status = 'Available'),
                        2) as starting_price
                from catalog_sets s
                join themes t on t.id = s.theme_id
                left join copies c on c.catalog_set_id = s.id
                left join grade_multipliers m on m.grade = c.grade
                group by s.id, t.id;
                """);
```

And the reverse, as the **first** statement of `Down`, above EF's `DropTable`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Migrations/…_AddCatalogBrowse.cs — first statement in Down
            // First, not last. The view reads grade_multipliers, and Postgres will not drop a
            // table that a view depends on.
            migrationBuilder.Sql("drop view catalog_set_listings;");
```

Every line of that view is a rule from `docs/IDEA.md` or the architecture document. Read it on
camera, one clause at a time:

| Clause | The rule it is |
| --- | --- |
| `left join copies` | A set with **no copies** still shows. An inner join removes it (step 2's test) |
| `filter (where c.status = 'Available')` on the count | *"Reserved copies do not count — they are already spoken for."* Retired copies do not count either: episode 31 promised that retired boxes would not show up anywhere customers look, and this is that place |
| The same `filter` on `min(m.multiplier)` | The starting price is the cheapest **available** copy. *"It never advertises a price nobody can act on"* |
| `s.base_rental_price * min(m.multiplier)` | `min(base × multiplier) = base × min(multiplier)`, because a base price is never negative — `CatalogSet.Catalogue` guarantees that |
| `round(…, 2)` | Postgres rounds `numeric` half away from zero — the same as `Money`'s `MidpointRounding.AwayFromZero`. So the view and `PriceCalculator` agree to the cent |
| No `coalesce(…, 0)` on the price | When nothing is available, `min` of nothing is `null`, and the response says so |
| `group by s.id, t.id` | Grouped by the two primary keys. Every other `s.` and `t.` column is allowed in the `select` because Postgres knows a primary key determines the rest of its row |

**Caveman version:** view is saved question. Every time someone ask, Postgres answer fresh:
count free box, find cheapest free box price, round like Money round. No free box? Price say
nothing, not say zero. Zero is lie — zero mean free.

This is the architecture document's rule — *"derived values are computed, never stored"* — at work.
When episode 40 lets an admin change a multiplier, every starting price in the catalog changes on the
next query, with no bulk update and no row left behind.

**The cost of a view, said once:** Postgres will now refuse `ALTER COLUMN` on any column the view
reads — `catalog_sets.name`, say — until the view is dropped. A future migration that touches one of
those columns has to drop the view, make the change and recreate the view. That is a real tax on
schema changes, and it is also Postgres telling you exactly which public read a schema change is
about to affect.

Apply it:

```bash
dotnet ef database update --project src/Catalog/BrickShare.Catalog.Api
```

### The endpoint

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, from step 1
        group.MapGet("/themes", ListThemesAsync);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        group.MapGet("/themes", ListThemesAsync);
        group.MapGet("/sets", BrowseSetsAsync);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste below ListThemesAsync
    private static async Task<Ok<BrowseSetsResponse>> BrowseSetsAsync(
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        List<SetListingResponse> sets = await database.Listings
            .Select(listing => new SetListingResponse(
                listing.Id,
                listing.SetNumber,
                listing.Name,
                listing.ThemeName,
                listing.Year,
                listing.PieceCount,
                listing.MinimumAge,
                listing.MinimumRentalDays,
                listing.AvailableCount,
                listing.StartingPrice))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new BrowseSetsResponse(sets));
    }
```

Green.

No order and no limit yet — nothing has asked for either. Step 6 has a test that asks for both.

**Why the response is not the read model.** `SetListingResponse` has the same fields as
`CatalogSetListing`, and returning the entity would save a `Select`. It is kept apart for the same
reason `CatalogSetResponse` is kept apart from `CatalogSet`: **the view is shaped by what
Postgres can compute cheaply, and the response is shaped by what a client was promised.** Those are
the same today. The first time the view gains a column for a join or a sort, the response must not
gain it too.

---

## Step 5 — The cheapest copy you can actually have

Now the rule the view exists for. Two copies, both available: an Excellent and a Fair. Then the Fair
one goes out on rent.

Two helpers first — one in the shared flow file, because step 6 needs it too:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogFlow.cs — paste at the end of the CatalogFlow class
    /// <summary>
    /// One box of a catalogued set, in the grade given, through the registration endpoint.
    /// </summary>
    public static async Task<Guid> RegisterCopyAsync(
        this CatalogDatabase database, HttpClient client, Guid setId, string grade)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { new { grade, baselineWeightInGrams = 1000 } } });

        response.EnsureSuccessStatusCode();

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(database.Api.Json);

        Assert.NotNull(registered);

        return Assert.Single(registered.Copies).Id;
    }
```

And the test, with a private helper under it:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — a second [Fact], below the first
    [Fact]
    public async Task The_starting_price_is_the_cheapest_copy_that_is_available()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        await Database.RegisterCopyAsync(client, setId, "Excellent");
        Guid fair = await Database.RegisterCopyAsync(client, setId, "Fair");

        SetListingResponse both = await TitanicAsync(client);

        // 60.00 × 0.55 — the same number PriceCalculator.RentalPrice gives for a Fair copy.
        Assert.Equal(2, both.AvailableCount);
        Assert.Equal(33.00m, both.StartingPrice);

        await SendOnRentAsync(fair);

        SetListingResponse onlyExcellent = await TitanicAsync(client);

        // The Fair copy is still cheaper. It is also in somebody's living room.
        Assert.Equal(1, onlyExcellent.AvailableCount);
        Assert.Equal(51.00m, onlyExcellent.StartingPrice);
    }

    private async Task<SetListingResponse> TitanicAsync(HttpClient client)
    {
        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);

        return Assert.Single(page.Sets);
    }

    /// <summary>
    /// The same helper as RetireCopyTests, for the same reason: the rentals service that makes this
    /// transition does not exist. Twice is a coincidence; the third caller moves it to CatalogFlow.
    /// </summary>
    private async Task SendOnRentAsync(Guid copyId)
    {
        await using CatalogDbContext dbContext = Database.NewDbContext();
        Copy copy = await dbContext.Copies.SingleAsync(candidate => candidate.Id == copyId);

        copy.Reserve();
        copy.Collect();

        await dbContext.SaveChangesAsync();
    }
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — the usings, what they become
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
```

The view was written in step 4, so this test is expected to pass on arrival — it describes a rule
rather than driving one. Run it.

**Red. `Assert.Equal() Failure: Expected 33.00, Actual (null)`.** Two copies available, the count
is right, and the price is missing.

**This is not a TDD red, and it is worth stopping the recording for.** The view is correct. Run the
same `GET` against the Compose database through the `.http` file and the price is there. The
difference is in the test harness:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/DatabaseTest.cs — already there, do not change it
    // Reset before, not after. See step 5.
    public Task InitializeAsync()
    {
        Database.Rebrickable.Reset();
        return Database.ResetAsync();
    }
```

Before **every** test, Respawn truncates every table in `public`. `MigrateAsync` inserted the four
multiplier rows once, when the container started; the first reset deleted them; the view's
`left join grade_multipliers` now finds nothing, and `min` of nothing is `null`. The test is
right, the view is right, and the harness is treating **reference data** as if it were **test
data**.

**Caveman version:** before each test, cleaner wipe whole cave. Good for mess test make. Bad for
price list painted on wall at start. Price list gone, cheapest price gone. Tell cleaner: not this
wall.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs — the anchor, already there
            // Clear this and the database forgets it has a schema, so the next `dotnet ef`
            // command tries to apply InitialCatalog to a database that already has the table.
            TablesToIgnore = ["__EFMigrationsHistory"]
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs — what it becomes
            // Clear this and the database forgets it has a schema, so the next `dotnet ef`
            // command tries to apply InitialCatalog to a database that already has the table.
            // grade_multipliers is reference data a migration seeds: tests read it, and none of
            // them own it. Clear it and every price in the catalog becomes null.
            TablesToIgnore = ["__EFMigrationsHistory", "grade_multipliers"]
```

Green. Step 2's test is still green too — it was green for the wrong reason before (no copies means
no price, with or without multipliers) and is green for the right one now.

The distinction this red teaches is real in every system with seeded data: **tables a migration
fills are part of the schema, not part of a test's state.** The rule that follows is a
consequence of the fix: a test that edits `grade_multipliers` — episode 40 will have them — must
put the table back itself, because nothing else will.

### What this test pins

This is the test that stops the formula living in two places from becoming two formulas. `33.00` and
`51.00` are the numbers `PriceCalculator.RentalPrice` gives for a 60.00 base at Fair and Excellent
— the unit tests in `RentalPriceTests` compute the same arithmetic in C#. If someone changes
rounding in `Money`, or changes the view to `round(…, 1)`, one side of that pair goes red.

Note what the assertion does **not** say: *the cheapest copy*. The Fair copy is still the cheapest
box the shop owns. It is just not one a customer can reserve. That is the line in UC-7 that a query
written from intuition gets wrong, and the second half of this test exists to catch exactly that.

> **Seam — 34a ends here.** If recording in two parts: every test is green, a customer can list the
> catalog and see real prices, and the only thing missing is the ability to narrow it down.

---

## Step 6 — Red → green: filters

Five filters from UC-7.2 and a limit. One `[Theory]`, three sets on the shelf, and each row is a query
string and the set numbers that should come back, in order.

| | Pieces | Age | Theme | Copies | Starting price |
| --- | --- | --- | --- | --- | --- |
| Concorde `10318-1` | 2083 | 18 | Icons | one Fair | 25.00 × 0.55 = **13.75** |
| Main Street Building `41704-1` | 1682 | 8 | Friends | none | **null** |
| Titanic `10294-1` | 9092 | 18 | Icons | one New | 60.00 × 1.00 = **60.00** |

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — below the step-5 test
    [Theory]
    [InlineData("", new[] { "10318-1", "41704-1", "10294-1" })]
    [InlineData("?minPieces=2000", new[] { "10318-1", "10294-1" })]
    [InlineData("?maxPieces=2083", new[] { "10318-1", "41704-1" })]
    [InlineData("?age=8", new[] { "41704-1" })]
    [InlineData("?availableNow=true", new[] { "10318-1", "10294-1" })]
    [InlineData("?maxPrice=20", new[] { "10318-1" })]
    [InlineData("?maxPrice=100", new[] { "10318-1", "10294-1" })]
    [InlineData("?age=18&minPieces=5000", new[] { "10294-1" })]
    [InlineData("?limit=1", new[] { "10318-1" })]
    public async Task Filters_narrow_the_catalog_and_the_order_is_by_name(
        string query, string[] expectedSetNumbers)
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets{query}", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal(expectedSetNumbers, page.Sets.Select(set => set.SetNumber));
    }

    [Fact]
    public async Task Filtering_by_theme_uses_the_id_from_the_filter_list()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        ThemesResponse? themes =
            await client.GetFromJsonAsync<ThemesResponse>("/api/v1/catalog/themes", Database.Api.Json);

        Assert.NotNull(themes);

        Guid icons = themes.Themes.Single(theme => theme.Name == "Icons").Id;

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?themeId={icons}", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal(new[] { "10318-1", "10294-1" }, page.Sets.Select(set => set.SetNumber));
    }

    private async Task StockTheShelfAsync(HttpClient client)
    {
        Guid titanic = await Database.CatalogueTitanicAsync(client);
        Guid concorde = await Database.CatalogueAsync(client, StockedSet.Concorde);
        await Database.CatalogueAsync(client, StockedSet.MainStreetBuilding);

        await Database.RegisterCopyAsync(client, titanic, "New");
        await Database.RegisterCopyAsync(client, concorde, "Fair");
    }
```

The theme test is a `[Fact]` rather than a row because a theme id is a `Guid` minted at runtime,
and an attribute cannot hold one. It gets it the way a real client would: from the filter list. That
is step 1 and step 6 tested as the pair they are.

Red, and for a clear reason: **every row except the first returns all three sets**, because the
handler ignores its query string. The first row is red as well — it gets the sets in the order they
were catalogued, Titanic first, and the test wants them by name.

**Caveman version:** ask for small set — get all set. Ask for child set — get all set. Handler
not listen. Also list come back in random order. Test say: listen, and sort.

Green. The filter record, in its own file, because the validator in step 7 joins it:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — new file
namespace BrickShare.Catalog.Api.Endpoints;

/// <summary>
/// What a customer can narrow the catalog by. Every filter is optional, and absent means
/// "do not filter" — never "filter on the default".
/// </summary>
/// <param name="ThemeId">An id from GET /catalog/themes.</param>
/// <param name="MinPieces">At least this many pieces.</param>
/// <param name="MaxPieces">At most this many pieces.</param>
/// <param name="Age">The builder's age. A set rated 8+ suits an eight-year-old; one rated 18+ does not.</param>
/// <param name="MaxPrice">Compared against the starting price, so a set with nothing available never matches.</param>
/// <param name="AvailableNow">True keeps only sets with a copy available now. False is not a filter.</param>
/// <param name="Limit">How many sets to return, 1 to 50. 24 when absent.</param>
public sealed record BrowseQuery(
    Guid? ThemeId,
    int? MinPieces,
    int? MaxPieces,
    int? Age,
    decimal? MaxPrice,
    bool? AvailableNow,
    int? Limit)
{
    public const int DefaultLimit = 24;
    public const int MaxLimit = 50;
}
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — BrowseSetsAsync, replacing the step-4 version
    private static async Task<Ok<BrowseSetsResponse>> BrowseSetsAsync(
        [AsParameters] BrowseQuery query,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        IQueryable<CatalogSetListing> listings = database.Listings;

        if (query.ThemeId is { } themeId)
        {
            listings = listings.Where(listing => listing.ThemeId == themeId);
        }

        if (query.MinPieces is { } minPieces)
        {
            listings = listings.Where(listing => listing.PieceCount >= minPieces);
        }

        if (query.MaxPieces is { } maxPieces)
        {
            listings = listings.Where(listing => listing.PieceCount <= maxPieces);
        }

        if (query.Age is { } age)
        {
            listings = listings.Where(listing => listing.MinimumAge <= age);
        }

        if (query.MaxPrice is { } maxPrice)
        {
            // A null starting price never compares true, so this also keeps only sets with a
            // copy available. Intended: a price nobody can act on is not a price.
            listings = listings.Where(listing => listing.StartingPrice <= maxPrice);
        }

        if (query.AvailableNow is true)
        {
            listings = listings.Where(listing => listing.AvailableCount > 0);
        }

        List<SetListingResponse> sets = await listings
            .OrderBy(listing => listing.Name)
            .ThenBy(listing => listing.Id)
            .Take(query.Limit ?? BrowseQuery.DefaultLimit)
            .Select(listing => new SetListingResponse(
                listing.Id,
                listing.SetNumber,
                listing.Name,
                listing.ThemeName,
                listing.Year,
                listing.PieceCount,
                listing.MinimumAge,
                listing.MinimumRentalDays,
                listing.AvailableCount,
                listing.StartingPrice))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new BrowseSetsResponse(sets));
    }
```

Green: nine rows and the theme test.

Every `<param>` on the record is documented, not only the interesting three. With
`GenerateDocumentationFile` on and warnings as errors, documenting *some* parameters of a
record is warning CS1573, and the build fails.

**Caveman version:** each filter customer give, add one sieve. No filter, no sieve. At end, sort
by name, take first handful. Database do all sieve in one go — nothing come back to server just
to be thrown away.

### The decisions hiding in those `if`s

**`age` is the builder's age, not a rating.** `?age=8` means *"my daughter is eight"* and returns
sets rated for eight or under. The alternative, `?minimumAge=8`, means *"sets rated exactly 8+"*,
which is a question about packaging, not about a child. **Name a filter after the question the
customer is asking, not after the column it reads.**

**`maxPrice` quietly means *available*.** Main Street Building has no price and does not appear in
`?maxPrice=100`, although nothing about it costs more than 100. That is a choice, and there is a
defensible opposite: *"no price"* could pass every price filter. It is rejected because the
customer asked *what can I rent for under 100*, and the answer to that cannot include a set they
cannot rent at all. The row `?maxPrice=100` exists to pin this down, so that nobody "fixes" it
later without deciding to.

**No `minPrice`.** Nobody shops for *"at least 30"*. If somebody asks, it is four lines.

**`availableNow=false` is not a filter.** It does not mean *"only sets I cannot have"*; nobody wants
that list. `false` and absent behave the same, and the parameter's documentation says so. A `bool?`
whose `false` did something would be the more surprising API.

**`ThenBy(listing => listing.Id)`.** Two sets can share a name — LEGO has reused plenty. Ordering by
name alone leaves their relative order to Postgres, which may differ between two identical queries.
The id tie-break makes the order **total**, which does not matter much today and is load-bearing in
episode 36: keyset paging needs an order in which every row has exactly one position.

**The composition happens in `IQueryable`, not in a list.** Each `Where` adds a clause to one SQL
statement; nothing runs until `ToListAsync`. This is exactly what the view bought in step 4 — every
filter here is on a plain column, including the two that are aggregates underneath.

---

## Step 7 — Red → green: refusing nonsense filters

`?minPieces=3000&maxPieces=2000` currently returns an empty list, which reads as *"we have nothing
for you"* when the truth is *"you asked for something that cannot exist"*. And `?limit=100000` is
a request to serialise the whole catalog.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — below the theme test
    [Theory]
    [InlineData("?minPieces=3000&maxPieces=2000", "maxPieces")]
    [InlineData("?limit=51", "limit")]
    [InlineData("?age=19", "age")]
    [InlineData("?maxPrice=-1", "maxPrice")]
    public async Task A_filter_that_cannot_match_anything_is_refused(string query, string field)
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/catalog/sets{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(field, problem.Errors.Keys);
    }
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — the usings, what they become
using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
```

Red: four `200`s.

**Caveman version:** man ask "set bigger than three thousand and smaller than two thousand".
Computer say "none". True, but useless — man think shop empty. Better say: "your question
broken, here which part".

Green is a validator next to the record, and the filter episode 30 built:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — the usings, at the top of the file
using FluentValidation;
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — at the end of the file
public sealed class BrowseQueryValidator : AbstractValidator<BrowseQuery>
{
    public BrowseQueryValidator()
    {
        RuleFor(query => query.MaxPieces)
            .GreaterThanOrEqualTo(query => query.MinPieces)
            .When(query => query.MinPieces is not null)
            .WithMessage("maxPieces is below minPieces, so no set could match.");

        RuleFor(query => query.Age).InclusiveBetween(0, 18)
            .WithMessage("An age is between 0 and 18. Every set rated for adults is 18+.");

        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0m)
            .WithMessage("A price cannot be negative.");

        RuleFor(query => query.Limit).InclusiveBetween(1, BrowseQuery.MaxLimit)
            .WithMessage($"A page holds 1 to {BrowseQuery.MaxLimit} sets.");
    }
}
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, from step 4
        group.MapGet("/sets", BrowseSetsAsync);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        group.MapGet("/sets", BrowseSetsAsync)
            .AddEndpointFilter<ValidationFilter<BrowseQuery>>();
```

Green. No registration: `AddValidatorsFromAssemblyContaining<Program>` in `Program.cs` finds
`BrowseQueryValidator` by itself. That was the trade episode 30 made — less explicit, and this is the
first time it paid out without anyone touching `Program.cs`.

Every rule on a nullable property passes when the value is absent: FluentValidation's comparison
rules skip `null`. `MaxPieces` also needs `When(MinPieces is not null)`, because a comparison
against a missing value has nothing to compare with.

**The filter works with `[AsParameters]` because of how episode 30 wrote it.** `ValidationFilter`
looks for an argument **of type** `BrowseQuery`, and `[AsParameters]` hands the handler exactly
one of those — built from the query string. A filter that looked for a JSON body would have
needed a second version for `GET`s. Episode 30 attached it **per endpoint** rather than per group
for the same reason — *"it is wrong the moment episode 34 adds a `GET`"* — and this is that `GET`,
with the filter attached where its request is.

**Caveman version:** guard at door check question before question go in. Guard not care if
question come in body or in address — guard look for question-shape thing. Found it, check it.

### Why a limit, and why 50

`limit` has a default of 24 and a ceiling of 50, and neither number is precious. 24 fills a grid
of 2, 3, 4 or 6 columns without a ragged last row, and 50 is enough for any screen and small enough
that one request cannot turn into a slow query. **What matters is that there is a ceiling.** An API
without one has handed every caller a way to make it do the most expensive thing it can do.

And there is **no page two** yet. `?limit=24` returns the first 24 sets by name, and nothing
returns the 25th. Episode 36 fixes that with keyset paging, and the argument for keyset over
`?page=2` needs search in the room to be worth having. Say so on camera — a list with no page two
is an honest gap, not a finished feature.

---

## Step 8 — The indexes we are not adding

**No code in this step.** Its output is a decision, and the evidence for it is a query plan.

The course plan said this episode teaches *"indexing for the filters"*. This is what that means in
practice: **look before you index**, and at this size, decline. Against the Compose database, with
the sets from *Before recording* in it:

```sql
\d+ catalog_set_listings

explain analyze
select * from catalog_set_listings
where piece_count >= 2000
order by name, id
limit 24;
```

Read the plan from the bottom up and find the line that says `Filter: (piece_count >= 2000)`. It
sits on the **scan of `catalog_sets`**, below the aggregate. Postgres has pushed the `WHERE`
through the view: the sets are narrowed **first**, and only the survivors get their copies counted.
That is the view behaving like a table, which is what step 4 was betting on.

Then look at what it scanned: `Seq Scan on catalog_sets`. Now add the index a reflex would add, and
ask again:

```sql
create index ix_scratch_piece_count on catalog_sets (piece_count);

explain analyze
select * from catalog_set_listings where piece_count >= 2000 order by name, id limit 24;
```

**Still a sequential scan.** The planner has the index and declines it, and it is right to. The
whole table is one or two 8 KB pages. Reading them in order costs less than reading an index and
then jumping into the table for every match. Force its hand, to prove the index *could* be used:

```sql
set enable_seqscan = off;
explain analyze
select * from catalog_set_listings where piece_count >= 2000 order by name, id limit 24;
reset enable_seqscan;

drop index ix_scratch_piece_count;
```

Now it uses the index, and the `actual time` is no better, and often worse. Drop the index on camera.

**Caveman version:** cave has twelve rock. Look at all twelve — fast. Build map of rock first,
then read map, then walk to rock — slower. Map good for thousand-rock cave. Not this cave. Build
map when cave big, not before.

### What decides it, filter by filter

| Filter | Index? | Why |
| --- | --- | --- |
| `themeId` | **Already has one** | `ix_catalog_sets_theme_id`, from episode 33 — not for this filter, but because it is a foreign key and every join through it uses it |
| `minPieces` / `maxPieces` | No | A range that matches a large share of the catalog. Even at scale, *"at least 1000 pieces"* is most sets, and the planner would still scan |
| `age` | No | Nineteen possible values, and most sets share a handful of them. Too few distinct values for an index to narrow anything |
| `maxPrice`, `availableNow` | **Cannot have one** | They filter on aggregates. The number does not exist until the copies have been counted, so there is nothing to index |

That last row is the one to linger on. **No index can help a filter on a value computed per query.**
If price and availability filters ever get slow, the fix is not an index. It is the escape route the
architecture document names: a **per-set summary table**, maintained whenever a copy changes status,
so `available_count` and `starting_price` become real columns. That is a new table, an update on
every status change, and a consistency problem to own. Worth knowing; not worth building before
it is needed.

### When to come back

At a few thousand sets, none of this matters. The things to watch for are:

- `pg_stat_statements` showing the browse query near the top of total time;
- the catalog growing into the tens of thousands of sets;
- a filter being added that is **selective** — something that matches 1% of rows, not 60%.

Any one of those is a reason to run this `explain` again. **An index is a write cost you pay on
every insert, forever, for a read benefit you should be able to see in a plan.** Here you cannot.

---

## Step 9 — Describe it, and run it

**Not driven by a test.** OpenAPI metadata and a request collection.

Episode 32's rule was that every status code an endpoint can answer with is in the document. The two
new routes need their summaries, and `/catalog/sets` needs its `400`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, from step 7
        group.MapGet("/themes", ListThemesAsync);
        group.MapGet("/sets", BrowseSetsAsync)
            .AddEndpointFilter<ValidationFilter<BrowseQuery>>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        group.MapGet("/themes", ListThemesAsync)
            .WithSummary("List the themes a customer can filter by")
            .WithDescription("Every theme with at least one catalogued set, by name.");

        group.MapGet("/sets", BrowseSetsAsync)
            .AddEndpointFilter<ValidationFilter<BrowseQuery>>()
            .WithSummary("Browse the catalog")
            .WithDescription(
                "Sets by name, filtered by theme, piece count, age, price and availability. "
                + "startingPrice is the cheapest copy available now, and null when none is. "
                + "maxPrice therefore returns only sets with a copy available.")
            .ProducesValidationProblem();
```

The OpenAPI document's description still says the service is staff-facing, and it no longer is.
Then there is a comment in `Program.cs` that has promised the wrong episode since it was written.
Observability is episode 41, not this one:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — the anchor, already there
        document.Info.Description =
            "Staff-facing catalog service: look a set up, catalogue it, register copies of it, retire a copy.";
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — what it becomes
        document.Info.Description =
            "The catalog service. Staff look a set up, catalogue it, register and retire copies; "
            + "customers browse and filter what the shop stocks.";
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — the anchor, already there
        // One id that appears in the response and in the logs, so a screenshot from a staff
        // member is enough to find the request. Episode 34 wires the other end of this.
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — what it becomes
        // One id that appears in the response and in the logs, so a screenshot from a staff
        // member is enough to find the request. Episode 41 wires the other end of this.
```

**Caveman version:** map of API must say new doors exist, and what bad knock look like. And old
note on wall said "episode 34 do this" — wrong. Fix note. Wrong note worse than no note.

Then the requests, appended to the end of `BrickShare.Catalog.Api.http`:

```http
### BrickShare.Catalog.Api.http — append at the end of the file

### The filter list. Themes with at least one set, by name.
GET {{host}}/api/v1/catalog/themes

### The whole catalog, first 24 by name. Look at availableCount and startingPrice.
GET {{host}}/api/v1/catalog/sets

### Paste a theme id from the filter list.
@themeId = paste-a-theme-id-from-the-filter-list
GET {{host}}/api/v1/catalog/sets?themeId={{themeId}}

### For an eight-year-old, with a copy on the shelf today.
GET {{host}}/api/v1/catalog/sets?age=8&availableNow=true

### Under 40, which also means: available now.
GET {{host}}/api/v1/catalog/sets?maxPrice=40

### A question with no possible answer. 400, and errors.maxPieces says why.
GET {{host}}/api/v1/catalog/sets?minPieces=3000&maxPieces=2000
```

Run them, and finish on `/scalar/v1`: a new **Browse** tag with two operations, the query parameters
of `BrowseQuery` listed one by one, and `startingPrice` shown as nullable. **The document now has two audiences, and the tags say which
routes are whose.**

---

## What this episode is not

**No counts next to the themes.** The filter list says *Icons*, not *Icons (12)*, and nothing
tells the customer that Friends has no set under the price cap they just set. That is
**episode 35**, and it depends on the filter chain this episode finished.

**No search.** Not by name, not by set number — *"Titanc"* finds nothing, because nothing looks.
Episode 36 is `pg_trgm`, and the argument for **not** adding Azure AI Search.

**No page two.** `limit` caps the list and nothing continues it. Episode 36, with keyset paging —
which is why step 6 made the order total.

**No set detail, no list of copies, no scan by label.** A customer can see that the Titanic has one
copy available at 51.00, and cannot see which one or why. `GET /catalog/sets/{id}` with every copy,
its grade, price and deposit is **episode 37**, together with the staff `GET /catalog/copies/by-label/{code}`
and the `Location` headers that episodes 28–30 left pointing at routes that do not exist.
**The view built here is where episode 37 gets its per-set numbers**, so they cannot disagree with
this page.

**No multiplier edits.** Four seeded rows and no endpoint. Editing them re-prices the entire catalog,
which is why it is Admin-only and two-phase — **episode 40**.

**No caching.** A listing that changes whenever a copy goes out is a listing whose cache is wrong
exactly when it matters. At this size the query is cheap, so there is nothing to earn.

**No summary table.** Named in step 8, deliberately not built.

**No authorization — and this one stays.** Browse is anonymous on purpose: UC-7 has no rule that a
customer must sign in to look, and a rental shop that hides its stock behind a sign-up wall rents
less. Episode 40 secures the staff groups and leaves `MapBrowse` alone. That is why it is a separate
group.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green — one new in `ThemeListTests`, and in `BrowseSetsTests` two facts, a fact for the theme filter, and two theories of nine and four rows |
| `dotnet ef migrations has-pending-model-changes` | No changes. The view is `ToView`, so EF does not think it owes a `CreateTable` |
| `dotnet ef migrations list` | `AddCatalogBrowse` last |
| `\d grade_multipliers` | `grade` primary key, `multiplier numeric(4,2)`, `ck_grade_multipliers_positive` |
| `update grade_multipliers set multiplier = 0 where grade = 'Fair'` | Refused by the check constraint |
| `dotnet test` twice in a row | Green both times. The seed survives every Respawn reset, not only the first |
| `\d+ catalog_set_listings` | The view definition, exactly as the migration wrote it |
| `database update AddThemes`, then `database update` again | Both succeed. The `Down` dropped the view **before** the table |
| `explain analyze` from step 8 | The piece-count filter on the `catalog_sets` scan, below the aggregate |
| `GET /catalog/sets` on a set with no copies | `availableCount: 0`, `startingPrice: null` |
| `GET /catalog/sets?minPieces=3000&maxPieces=2000` | `400`, `errors.maxPieces` |
| `/openapi/v1.json` | Six operations under four tags, `startingPrice` nullable |
| `git diff --stat src/Catalog/BrickShare.Catalog.Domain` | Nothing. The domain did not change |

The row to run on camera is the `Down` round-trip. **A migration with hand-written SQL in it is the
one place where the tooling cannot check the work for you**, and the order of two statements in
`Down` is the difference between a rollback and a stuck deploy.

## Next

[Episode 35 — How many sets behind each theme](catalog-api.md#episode-35--how-many-sets-behind-each-theme):
a count next to every theme, of the sets choosing it would return **given the other filters already
set**. It is one count per theme over the view this episode built, plus the rule that makes a facet
count useful: it applies every filter except its own.
