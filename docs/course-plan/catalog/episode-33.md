# Episode 33 — Themes of our own

← [Course plan](catalog-api.md) · Previous: [Episode 32 — What this API says about itself](episode-32.md)

Episode 27 wrote a sentence and walked away from it:

> **No theme entity.** `theme_name` is text, copied from Rebrickable, and episode 34 will filter the
> public catalog on it. Free text is a bad thing to filter on, and **episode 33** replaces it with a
> `themes` table, a foreign key and a migration that backfills from the column this episode writes.
> The nested-theme question — `parent_id` — belongs to that episode too.

This is that episode, and it is the first one in the course where a migration **moves rows that
already exist**. Everything before it created empty tables, or added columns to tables that were
empty in every environment. Episode 29 said so out loud and promised the real thing here:

> Add the column nullable, backfill it in a second statement, then alter it to not null — three
> migrations' worth of thinking in one file. Episode 33 does exactly that for themes, against data
> that actually exists.

So the episode is three arguments and one careful file. **Why a foreign key rather than free text**,
which the next episode makes concrete. **Which theme a set actually belongs to**, when the upstream
data is a tree and the shop's customers are not — a product decision wearing a mapping decision's
clothes. And **the order of operations in a data migration**, which is the part that is invisible
until the day it runs against a database somebody is using.

**Done when** `catalog_sets` carries a theme id, no theme name is stored twice, and the migration
runs green against a database that already holds sets.

> **Runtime: about 28 minutes as written — roughly double the budget, and this one should be
> recorded as two.** The seam is between step 7 and step 8 and it is completely clean, because the
> two halves answer different questions:
>
> - **33a — "Themes of our own" (steps 1–7, ~17 minutes).** The table, the foreign key, the data
>   migration and the handler that adopts a theme. Ends with the schema changed and every test green.
> - **33b — "Which theme is it really?" (steps 8–10, ~11 minutes).** `parent_id`, the walk up the
>   tree, the loop guard, and the product argument about which level customers browse.
>
> Recording 33a short, if it comes to that: step 1 is the cut, down to the two `select` statements
> and the sentence under them. **Step 6 is not cuttable** — it is the reason the episode exists.
> If you record it as one episode, the numbering of episodes 34 onward does not move either way.

## Before recording

- Episode 32 merged: `/openapi/v1.json` describes four operations, `/scalar/v1` renders them, and
  the whole suite is green.
- `docker compose up` for Postgres, `dotnet user-secrets` still holding your Rebrickable key.
- A branch. **There is a migration this episode**, and it is the whole middle of it.
- **A database with rows in it.** This is the one episode whose subject does not exist on an empty
  schema. Before you hit record: catalogue two or three sets through the API — the Titanic from
  episode 27, plus at least one more — so that step 1's `select` has something to show and step 6's
  backfill has something to move. A migration demo against zero rows teaches nothing.
- `docs/course-plan/catalog/episode-27.md` open at *"No theme entity"* and `episode-29.md` open at
  the `defaultValue: new Guid("0000…")` block. Both are promises this episode keeps, and both read
  better on screen than summarised.
- `docs/IDEA.md` open at UC-7.2 — *filters on theme, piece count, age rating, price and
  availability*. The filter list is the reason any of this happens.

**Two of the ten steps are not driven by a test, and both say so where they appear**: step 6 is a
migration — infrastructure, in the `CLAUDE.md` sense — and step 10 is a request collection.
Everything else goes red first. **One test in step 9 passes the moment it is written** and says so.

### Two things go red on purpose and stay red for a while

Say both out loud when they open, and again when they close, or the screen looks like a mess rather
than like a plan.

| From | Until | What is red, and why |
| --- | --- | --- |
| Step 3 | Step 5 | **The Api does not compile.** `CatalogSet.Catalogue` takes a `Theme` and the endpoint still hands it a `string`. |
| Step 4 | Step 6 | **The integration suite fails wholesale.** The model wants `theme_id`; the database still has `theme`. Not a TDD red — a schema that has not caught up. |

The first of those is why the migration is step 6 and not step 5: **`dotnet ef migrations add`
builds the startup project before it does anything else.** A model that does not compile cannot be
diffed, so the compiler has to be green before EF is asked a question. The unit tests are unaffected
throughout — `BrickShare.Catalog.UnitTests` references `Domain` and nothing else, which is exactly
why the red/green loop in steps 2 and 3 still works while the Api is broken.

Every sample below names its file and where in it the code goes. Where something is edited in a file
that already exists, the **first block is what is already there** — the anchor to find on screen —
and the **second block is what to paste**. Every block is copy-paste clean: no markers, no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/UnitTests/ThemeTests.cs` | **New** — three tests |
| `src/…/Domain/Theme.cs` | **New** — the entity |
| `src/…/Domain/CatalogSet.cs` | `string Theme` becomes `Guid ThemeId` |
| `tests/…/UnitTests/CatalogSetTests.cs` | One test, and the shared `Catalogue` helper |
| `src/…/Api/Persistence/ThemeConfiguration.cs` | **New** |
| `src/…/Api/Persistence/CatalogSetConfiguration.cs` | One property replaced, one foreign key added |
| `src/…/Api/Persistence/CatalogDbContext.cs` | One `DbSet` |
| `src/…/Api/Endpoints/CatalogSetEndpoints.cs` | Adopt-or-find the theme, and a second constraint name |
| `tests/…/IntegrationTests/Persistence/CopyPersistenceTests.cs` | The helper needs a theme now |
| `src/…/Api/Migrations/…_AddThemes.cs` | **Generated, then rewritten.** The centre of the episode |
| `tests/…/IntegrationTests/CatalogSets/ThemeTests.cs` | **New** — two tests |
| `src/…/Api/Rebrickable/RebrickableTheme.cs` | **33b** — `ParentId` stops being ignored |
| `src/…/Api/Rebrickable/RebrickableClient.cs` | **33b** — one mapped field |
| `src/…/Api/Endpoints/CatalogLookupEndpoints.cs` | **33b** — the walk up the tree |
| `tests/…/IntegrationTests/CatalogSets/LookupTests.cs` | **33b** — three tests |
| `src/…/Api/BrickShare.Catalog.Api.http` | Requests appended at the end |

---

# 33a — Themes of our own

## Step 1 — The filter that does not work

No code in this step. `docker compose up`, and ask the database the question episode 34 is going to
have to answer for a customer.

```sql
select distinct theme from catalog_sets order by theme;

select id, set_number, name, theme from catalog_sets;
```

Two or three names come back and they look fine. Now say what is actually there: **a string, copied
from a third party, once per set, with nothing anywhere that says two of them are the same thing.**

Three things go wrong with that, and none of them is hypothetical.

| What happens | The text column | A themes table |
| --- | --- | --- |
| Rebrickable renames *Creator Expert* to *Icons* | Sets catalogued before the rename say one thing, after it say another. The filter list now has two entries for one theme, and neither shows all the sets | One row, one `update`, every set follows it |
| A customer filters the catalog | `where theme = 'Icons'` — an exact match against a value nobody can enumerate. Where does the UI get the list of options? `select distinct` over the whole table | `select * from themes` — a short, stable list with ids in it |
| Somebody types `icons ` with a trailing space | A fourth theme | Impossible. There is no place to type it |

**Caveman version:** shop write theme name on every box, by hand, each time. Big pile of boxes, one
word spelled three way. Customer say "show me Icons box". Shop look at pile, find only some. Better:
shop keep one list of theme. Box point at list. Change name on list, every box change too.

### The rule underneath

A value that appears in more than one row and means the same thing every time is **not a value, it
is a reference**. That is the whole of it, and it is worth saying in exactly those words because it
is the question to ask of every text column anyone ever writes: *could two rows disagree about the
spelling of something they both mean?* If yes, it is a foreign key waiting to happen.

The counter-argument is real and gets its ten seconds: this is one extra table, one extra join in
every read, and about sixty lines of migration for a shop with a few hundred sets in it. **For a
column nobody filters on — say, a free-text internal note — the text column is correct and
normalising it is over-engineering.** Theme is different precisely because UC-7.2 makes it a filter,
and the next episode has to build that filter out of whatever this episode leaves behind.

---

## Step 2 — Red: a theme the shop owns

The domain has no idea what a theme is. Write the test that says it should.

```csharp
// tests/BrickShare.Catalog.UnitTests/ThemeTests.cs — new file
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class ThemeTests
{
    [Fact]
    public void An_adopted_theme_keeps_the_upstream_id_it_came_from()
    {
        Theme theme = Theme.Adopt(252, "Icons");

        Assert.Equal(252, theme.RebrickableId);
        Assert.Equal("Icons", theme.Name);
        Assert.NotEqual(Guid.Empty, theme.Id);
    }

    [Fact]
    public void A_theme_without_a_name_is_not_a_theme()
    {
        Assert.Throws<ArgumentException>(() => Theme.Adopt(252, "  "));
    }

    [Fact]
    public void A_theme_id_Rebrickable_could_not_have_issued_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Theme.Adopt(0, "Icons"));
    }
}
```

**Red is a build error** — `Theme` does not exist — and that counts, exactly as it has since episode
12. Green is one file.

```csharp
// src/Catalog/BrickShare.Catalog.Domain/Theme.cs — new file
namespace BrickShare.Catalog.Domain;

/// <summary>
/// A theme the shop can group sets under and customers can filter on. Adopted from Rebrickable
/// rather than invented here: <see cref="RebrickableId"/> is what makes two catalogued sets agree
/// that they are the same theme, even if the name upstream changes afterwards.
/// </summary>
public sealed class Theme
{
    public const int MaxNameLength = 100;

    private Theme(int rebrickableId, string name)
    {
        Id = Guid.CreateVersion7();
        RebrickableId = rebrickableId;
        Name = name;
    }

    public Guid Id { get; }

    public int RebrickableId { get; }

    public string Name { get; }

    public static Theme Adopt(int rebrickableId, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rebrickableId, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Theme(rebrickableId, name);
    }
}
```

**Two identities on one row, and that is deliberate.** `Id` is ours — a v7 `Guid`, the same shape as
every other key in this service, the one a BrickShare URL will carry. `RebrickableId` is theirs, and
it exists to answer one question: *have we already got this theme?*

The obvious alternative is to skip our own key and make `rebrickable_id` the primary key. It is one
fewer column and it would work today. It is declined for a reason that is worth more than the
column it costs: **a primary key is what everything else in the system points at, and this one would
be issued by an organisation that does not know BrickShare exists.** The day the shop wants a theme
of its own — *Staff picks*, *Rainy Sunday* — a table keyed by Rebrickable has nowhere to put it.
Keeping our key means that day costs a nullable column, not a migration of every row that references
a theme.

**Caveman version:** two name for same thing. One name shop give. One name stranger give. Shop
point at own name. Stranger name only for know "same thing already here". Stranger go away one day —
shop name still good.

No `Rename` method. Rebrickable renaming a theme is a real event and handling it is real work — it
is a job, not a request, and nothing schedules jobs in this service yet. Writing the method now
would be a method with no caller and no test that means anything. **Named, not built.**

---

## Step 3 — Red: the set belongs to a theme, not to a word

```csharp
// tests/BrickShare.Catalog.UnitTests/CatalogSetTests.cs — add above the private Catalogue helper
    [Fact]
    public void A_catalogued_set_points_at_a_theme_rather_than_naming_one()
    {
        Theme icons = Theme.Adopt(252, "Icons");

        CatalogSet set = CatalogSet.Catalogue(
            SetNumber.Parse("10294-1"),
            "Titanic",
            icons,
            2021,
            9092,
            new Money(629.99m),
            new Money(60.00m),
            7,
            18);

        Assert.Equal(icons.Id, set.ThemeId);
    }
```

Red: `CatalogSet.Catalogue` takes a `string` and `CatalogSet` has no `ThemeId`. Green is a property
and a parameter.

```csharp
// src/Catalog/BrickShare.Catalog.Domain/CatalogSet.cs — the constructor, the property and the factory as they are now
    private CatalogSet(
        SetNumber number,
        string name,
        string theme,
        int year,
```

Three edits to that file, and nothing else in it moves.

```csharp
// src/Catalog/BrickShare.Catalog.Domain/CatalogSet.cs — 1 of 3: the private constructor
    private CatalogSet(
        SetNumber number,
        string name,
        Guid themeId,
        int year,
        int pieceCount,
        Money retailPrice,
        Money baseRentalPrice,
        int minimumRentalDays,
        int minimumAge)
    {
        Id = Guid.CreateVersion7();
        Number = number;
        Name = name;
        ThemeId = themeId;
        Year = year;
        PieceCount = pieceCount;
        RetailPrice = retailPrice;
        BaseRentalPrice = baseRentalPrice;
        MinimumRentalDays = minimumRentalDays;
        MinimumAge = minimumAge;
    }
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/CatalogSet.cs — 2 of 3: replaces `public string Theme { get; }`
    /// <summary>
    /// The theme this set belongs to, by id. There is no navigation property on purpose: no rule
    /// on this class needs the theme's name, and a navigation is an invitation to load one.
    /// <see cref="Copy"/> points at its set the same way, for the same reason — episode 29.
    /// </summary>
    public Guid ThemeId { get; }
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/CatalogSet.cs — 3 of 3: the factory
    public static CatalogSet Catalogue(
        SetNumber number,
        string name,
        Theme theme,
        int year,
        int pieceCount,
        Money retailPrice,
        Money baseRentalPrice,
        int minimumRentalDays,
        int minimumAge)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentOutOfRangeException.ThrowIfLessThan(pieceCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(retailPrice.Amount);
        ArgumentOutOfRangeException.ThrowIfNegative(baseRentalPrice.Amount);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumAge);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1);

        if (minimumRentalDays > MaximumRentalDays)
        {
            throw new DomainRuleViolationException(
                $"A set cannot require {minimumRentalDays} days. The shop rents for at most "
                + $"{MaximumRentalDays} days, so a longer minimum could never be met.");
        }

        return new CatalogSet(
            number, name, theme.Id, year, pieceCount,
            retailPrice, baseRentalPrice, minimumRentalDays, minimumAge);
    }
}
```

**The factory takes a `Theme`, the property stores a `Guid`, and the asymmetry is the point.** A
`Guid` parameter would accept `Guid.NewGuid()`, `set.Id`, or the id of a copy, and the compiler
would be delighted. A `Theme` parameter can only be satisfied by something that went through
`Theme.Adopt` — which means somebody, somewhere, has a theme in hand. **Take the strongest type you
can at the edge; store the narrowest thing you need in the middle.**

It is not a guarantee. A `Theme` that was never saved still hands over a `Guid` that no row has, and
the only thing that catches that is the foreign key in step 4. Say so — the type system narrows the
mistakes, the database refuses them.

### The fallout, which is the test suite telling the truth

`dotnet build` now fails in three places, and every one of them is a caller that was passing a word
where a thing belongs. That is not breakage, it is the compiler doing the search for us.

```csharp
// tests/BrickShare.Catalog.UnitTests/CatalogSetTests.cs — the existing private helper, replaced whole
    private static CatalogSet Catalogue(
        int minimumRentalDays = 7,
        Money? retailPrice = null) =>
        CatalogSet.Catalogue(
            SetNumber.Parse("10294-1"),
            "Titanic",
            Theme.Adopt(252, "Icons"),
            2021,
            9092,
            retailPrice ?? new Money(629.99m),
            new Money(60.00m),
            minimumRentalDays,
            18);
```

The other two callers are the endpoint and a test helper that also needs a row in the database, and
**both are fixed in step 5, together.** Leave them red across step 4 and say that you are: **a
compiler error is a perfectly good to-do list, and it is one that cannot be forgotten.**

It is also a to-do list with a deadline, which is the thing to flag here rather than at the end.
The migration in step 6 cannot be generated while this list is open — `dotnet ef` builds the Api
before it diffs anything — so the list closes first and the database changes second.

---

## Step 4 — The schema, in two files

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/ThemeConfiguration.cs — new file
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("themes");

        builder.HasKey(theme => theme.Id);
        builder.Property(theme => theme.Id).HasColumnName("id");

        // Unique, and this is the index the whole episode leans on: it is what stops two
        // requests adopting the same theme twice, and what makes "have we got this one?"
        // a single indexed lookup rather than a scan.
        builder.Property(theme => theme.RebrickableId).HasColumnName("rebrickable_id").IsRequired();

        builder.HasIndex(theme => theme.RebrickableId)
            .IsUnique()
            .HasDatabaseName("ix_themes_rebrickable_id");

        builder.Property(theme => theme.Name)
            .HasColumnName("name")
            .HasMaxLength(Theme.MaxNameLength)
            .IsRequired();

        // Not unique, deliberately. Episode 34 orders the filter list by this column, so the
        // index earns its place — but uniqueness here would mean the shop could never hold two
        // upstream themes that happen to share a name, and a rename upstream could fail a
        // migration. Identity is the id; the name is a label that happens to be unique today.
        builder.HasIndex(theme => theme.Name)
            .HasDatabaseName("ix_themes_name");
    }
}
```

That last comment is the close call of the episode and should be delivered as one. The course plan's
*done when* says **no theme name is stored twice**, and this configuration does not enforce it with
a constraint — it gets it as a consequence of deduplicating on the upstream id. If your instinct is
`.IsUnique()` on the name, it is a defensible instinct and the trade-off is the one in the comment.
**Say which way you went and why; do not pretend the other side is silly.**

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogSetConfiguration.cs — what is there now
        builder.Property(set => set.Theme)
            .HasColumnName("theme")
            .HasMaxLength(100)
            .IsRequired();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogSetConfiguration.cs — replaces those four lines
        builder.Property(set => set.ThemeId).HasColumnName("theme_id").IsRequired();

        // Same shape as the copies → sets relationship from episode 29: a foreign key with no
        // navigation property on either end, and Restrict rather than the convention's Cascade.
        // Deleting a theme that sets point at should fail loudly, not quietly delete the sets.
        builder.HasOne<Theme>()
            .WithMany()
            .HasForeignKey(set => set.ThemeId)
            .HasConstraintName("fk_catalog_sets_theme_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(set => set.ThemeId)
            .HasDatabaseName("ix_catalog_sets_theme_id");
```

**The index on the foreign key is not automatic in the sense people assume.** EF creates one for a
relationship it discovers by convention; naming it here means it is a decision with a name we chose,
and episode 34 filters on this exact column — `where theme_id = $1` over a few thousand rows is the
query that pays for it.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — add below the Sets line
    public DbSet<Theme> Themes => Set<Theme>();
```

---

## Step 5 — Green the compiler, before EF is asked anything

Two callers have been red since step 3, and until both are fixed this episode cannot continue at
all. Not "should not" — **cannot**: the next step runs `dotnet ef migrations add`, and that command
builds the startup project before it looks at a model. A broken Api means no migration, whatever the
model says.

That is a general rule worth stating in one line while it is on screen: **the tooling that reads
your model needs your model to build.** It applies to `migrations add`, to `dbcontext scaffold`, to
anything that loads your assembly to ask it a question — and it is the reason a rename that touches
an entity is always finished before it is migrated, never halfway through.

Fix the real caller first.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — what is there now
        CatalogSet catalogSet = CatalogSet.Catalogue(
            snapshot.Number,
            snapshot.Name,
            snapshot.ThemeName,
            snapshot.Year,
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replaces the CatalogSet.Catalogue call
        Theme theme = await AdoptThemeAsync(database, snapshot, cancellationToken);

        CatalogSet catalogSet = CatalogSet.Catalogue(
            snapshot.Number,
            snapshot.Name,
            theme,
            snapshot.Year,
            snapshot.PieceCount,
            new Money(request.RetailPrice),
            new Money(request.BaseRentalPrice),
            request.MinimumRentalDays,
            request.MinimumAge);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replaces the return statement
        return TypedResults.Created(
            $"/api/v1/sets/{catalogSet.Id}", CatalogSetResponse.From(catalogSet, theme));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — add below IsAlreadyCatalogued
    /// <summary>
    /// The theme this snapshot belongs to, inserted if the shop has never seen it before.
    /// Saved on its own, before the set: see the comment in the catch block.
    /// </summary>
    private static async Task<Theme> AdoptThemeAsync(
        CatalogDbContext database,
        RebrickableSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        Theme? existing = await database.Themes
            .FirstOrDefaultAsync(theme => theme.RebrickableId == snapshot.ThemeId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        Theme adopted = Theme.Adopt(snapshot.ThemeId, snapshot.ThemeName);
        database.Themes.Add(adopted);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsThemeAlreadyAdopted(ex))
        {
            // Two staff members catalogued two Icons sets in the same second. The other request
            // won the unique index, which is a fine outcome — this one wants the row they wrote.
            database.Entry(adopted).State = EntityState.Detached;

            adopted = await database.Themes
                .SingleAsync(theme => theme.RebrickableId == snapshot.ThemeId, cancellationToken);
        }

        return adopted;
    }

    private static bool IsThemeAlreadyAdopted(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_themes_rebrickable_id"
        };
```

`EntityState` needs `using Microsoft.EntityFrameworkCore;`, which the file already has.

### Two SaveChanges, and the trade-off that buys

The theme is committed before the set, in its own transaction. That is not an oversight and it has a
visible cost: if the set then fails — a duplicate set number, a 409 — the theme row stays behind with
no sets pointing at it.

**That orphan is harmless and the alternative is not.** One transaction for both would mean holding
the insert of a row that another concurrent request is also trying to insert, which is a
lock-ordering problem and a deadlock waiting for the week the shop catalogues a hundred sets. A
theme with no sets, meanwhile, shows up in exactly one place — episode 34's filter list — and that
query has to exclude empty themes anyway, because a filter option that returns nothing is a bug
regardless of how the row got there.

**Caveman version:** read-then-write always race. Two hunter see no fire, both make fire. Database
say "only one fire" — good. Loser not cry, loser use winner fire. Cost: sometime fire with nobody
sit at it. Fire cost nothing. Two hunter fight over one stick — that cost plenty.

### The response keeps its shape

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — the existing From factory on CatalogSetResponse
    public static CatalogSetResponse From(CatalogSet set) => new(
        set.Id,
        set.Number.Value,
        set.Name,
        set.Theme,
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replaces that factory
    public static CatalogSetResponse From(CatalogSet set, Theme theme) => new(
        set.Id,
        set.Number.Value,
        set.Name,
        theme.Name,
        set.Year,
        set.PieceCount,
        set.RetailPrice.Amount,
        set.BaseRentalPrice.Amount,
        set.MinimumRentalDays,
        set.MinimumAge);
```

**The wire contract does not change.** `theme` is still a string, still a name, still exactly what a
client saw yesterday — and the OpenAPI document episode 32 generated is still true without anybody
editing it. Schema changes and contract changes are different things, and conflating them is how
teams end up unable to normalise anything.

No `themeId` on the response, tempting as it is. Nothing can use it until there is an endpoint that
takes one, and that is episode 34's `GET /catalog/themes`. **A field with no consumer is a field you
have promised not to change, for free.**

### The last red caller

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Persistence/CopyPersistenceTests.cs — the existing helper
    private async Task<Guid> ACataloguedSetAsync()
    {
        CatalogSet set = CatalogSet.Catalogue(
            SetNumber.Parse("10294-1"), "Titanic", "Icons", 2021, 9092,
            new Money(629.99m), new Money(60.00m), 7, 18);
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Persistence/CopyPersistenceTests.cs — replaces that helper whole
    /// <summary>
    /// A set for copies to belong to — and now a theme for the set to belong to. Episode 29 added
    /// the first of those two rows to this helper for exactly the same reason.
    /// </summary>
    private async Task<Guid> ACataloguedSetAsync()
    {
        Theme theme = Theme.Adopt(252, "Icons");

        CatalogSet set = CatalogSet.Catalogue(
            SetNumber.Parse("10294-1"), "Titanic", theme, 2021, 9092,
            new Money(629.99m), new Money(60.00m), 7, 18);

        await using CatalogDbContext context = Database.NewDbContext();
        context.Themes.Add(theme);
        context.Sets.Add(set);
        await context.SaveChangesAsync();

        return set.Id;
    }
```

**Each foreign key added to this service makes its test fixtures one row longer, and that is the
cost being paid for the guarantee.** Name it. A student who thinks normalisation is free will be
surprised by this file; a student who was told it costs setup will recognise it.

### Where the build stands at the end of this step

```bash
dotnet build
dotnet test --filter FullyQualifiedName~UnitTests
```

**Build clean, unit tests green** — the first of the two reds closes here. **The integration suite
is still failing, wholesale**, and running it now is worth the thirty seconds it costs: every test
that touches a set fails with `column c.theme_id does not exist`. That is not a test telling you
about your code. That is the model and the database disagreeing, and the only thing that settles it
is the next step.

---

## Step 6 — The migration that moves real rows

The Api compiles again, which is the precondition this step has been waiting for since step 3.

**This step is not driven by a test, and that is the `CLAUDE.md` exemption for infrastructure.**
It is also the step where a mistake is most expensive, so it gets the most care: the generated file
is read out loud, then rewritten, and then run twice — forwards and backwards — against a database
with rows in it.

```bash
dotnet ef migrations add AddThemes \
  --project src/Catalog/BrickShare.Catalog.Api
```

Read what came out. EF produced a correct *diff* and a dangerous *plan*, and the difference between
those two words is the lesson.

```csharp
// src/Catalog/…/Migrations/…_AddThemes.cs — generated. Read it on camera, then throw it away.
            migrationBuilder.DropColumn(
                name: "theme",
                table: "catalog_sets");

            migrationBuilder.AddColumn<Guid>(
                name: "theme_id",
                table: "catalog_sets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
```

Three separate failures in eight lines, and they are worth naming one at a time:

1. **The theme names are dropped before anything reads them.** After that statement, the only place
   the mapping from set to theme exists is `rebrickable_snapshots` — and if it had not existed there
   either, the information would be gone.
2. **Every existing set gets `Guid.Empty` as its theme.** Episode 29 met this exact default on an
   empty table and left it alone. Here the table is not empty, and `Guid.Empty` is a foreign key
   pointing at a theme that does not exist, so the constraint refuses it and the deploy fails.
3. **The order is a diff, not a plan.** EF lists the operations in the order it found the
   differences. A data migration has to run in the order that keeps the data true at every point in
   between, because a migration is not atomic in the mind of whoever is watching it run.

**Caveman version:** EF look at old shape, look at new shape, say what different. EF never ask
"what happen to stuff already in box?" That question is ours. Move stuff first. Then throw old box
away.

### The one it gets right

```csharp
// src/Catalog/…/Migrations/…_AddThemes.cs — generated, and this part stays
            migrationBuilder.CreateTable(
                name: "themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rebrickable_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_themes", x => x.id);
                });
```

### Before rewriting it: ask the database whether the backfill is possible

The plan is to source the mapping from `rebrickable_snapshots`, which is the only table that holds
both the upstream theme id and the set number. That works only if every catalogued set has a
snapshot behind it — which has been true since episode 28 made `lookupId` mandatory, and was **not**
true before it. Do not assume it. Ask:

```sql
select count(*)
from catalog_sets s
where not exists (
    select 1 from rebrickable_snapshots r where r.set_number = s.set_number
);
```

Zero on a database that only ever saw the post-episode-28 API. Non-zero on one that was around
before, and the honest answer then is *this migration cannot run here until somebody decides what
those sets' themes are*. **A theme row called "Unknown" is the tempting fix and it is a lie**: it
becomes a filter option a customer can click, and it never goes away. If the count is not zero, the
migration below fails loudly on its last `alter` — which is the correct outcome, and step 6's demo.

### The rewritten migration

```csharp
// src/Catalog/BrickShare.Catalog.Api/Migrations/…_AddThemes.cs — replaces the whole generated file
using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rebrickable_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_themes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_themes_rebrickable_id",
                table: "themes",
                column: "rebrickable_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_themes_name",
                table: "themes",
                column: "name");

            // One row per upstream theme id, taking the most recently fetched name for it.
            // DISTINCT ON is what makes a rename harmless: two snapshots, two spellings, one row.
            migrationBuilder.Sql(
                """
                insert into themes (id, rebrickable_id, name)
                select distinct on (theme_id) uuidv7(), theme_id, theme_name
                from rebrickable_snapshots
                order by theme_id, fetched_at desc;
                """);

            // Nullable, on purpose. Every existing row gets its value from the statement below,
            // and the alter after that is what proves the statement reached all of them.
            migrationBuilder.AddColumn<Guid>(
                name: "theme_id",
                table: "catalog_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                update catalog_sets s
                set theme_id = t.id
                from (
                    select distinct on (set_number) set_number, theme_id
                    from rebrickable_snapshots
                    order by set_number, fetched_at desc
                ) latest
                join themes t on t.rebrickable_id = latest.theme_id
                where s.set_number = latest.set_number;
                """);

            // The assertion. If any set was left without a theme, Postgres refuses this and the
            // whole migration rolls back — no half-migrated schema, no "Unknown" theme.
            migrationBuilder.AlterColumn<Guid>(
                name: "theme_id",
                table: "catalog_sets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_sets_theme_id",
                table: "catalog_sets",
                column: "theme_id");

            migrationBuilder.AddForeignKey(
                name: "fk_catalog_sets_theme_id",
                table: "catalog_sets",
                column: "theme_id",
                principalTable: "themes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Last, and only now: the old column has been read twice and is no longer the truth.
            migrationBuilder.DropColumn(
                name: "theme",
                table: "catalog_sets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "theme",
                table: "catalog_sets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // A Down that drops the foreign key and leaves the names behind is not a Down.
            // Going back has to restore the data too, or it is a data-loss button with a
            // reassuring name on it.
            migrationBuilder.Sql(
                """
                update catalog_sets s
                set theme = t.name
                from themes t
                where t.id = s.theme_id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "theme",
                table: "catalog_sets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_catalog_sets_theme_id",
                table: "catalog_sets");

            migrationBuilder.DropIndex(
                name: "ix_catalog_sets_theme_id",
                table: "catalog_sets");

            migrationBuilder.DropColumn(
                name: "theme_id",
                table: "catalog_sets");

            migrationBuilder.DropTable(
                name: "themes");
        }
    }
}
```

### The five things in that file worth stopping on

**1. `uuidv7()`, not `gen_random_uuid()`.** Postgres 18 — which is what `docker-compose.yml` runs and
what `infra/main.tf` provisions — generates v7 UUIDs natively, the same shape `Guid.CreateVersion7()`
produces everywhere else in this codebase. Time-ordered keys keep the index appends at the right-hand
edge instead of scattering writes across the B-tree. **On Postgres 17 or earlier this line is
`gen_random_uuid()`** and the rows are fine; they are just not ordered. Say that, because students
will be on 16 and 17 for years.

**2. `distinct on (theme_id) … order by theme_id, fetched_at desc` is the rename argument made
executable.** Two snapshots of the Titanic, taken either side of *Creator Expert* becoming *Icons*,
carry the same `theme_id` and different `theme_name`. Without `distinct on`, that is two rows and the
unique index fails the migration. With it, it is one row carrying the newer name — which is exactly
the behaviour step 1 said a themes table would buy.

**3. The `update` joins on `set_number`, which is the only link that exists.** There is no
`snapshot_id` on `catalog_sets` — episode 28 read the snapshot server-side and did not keep a
pointer to it. Worth admitting on camera as something we would now do differently; a column there
would have made this `update` a two-line join with no `distinct on` in it. **A migration is where
you find out which relationships you forgot to record.**

**4. The `alter … set not null` is the test this step does not have.** Migrations do not get unit
tests, and pretending otherwise would be theatre. What they get instead is a constraint placed
immediately after the statement it is checking, inside the same transaction, so a backfill that
missed rows cannot reach production as silent nulls. **Order the operations so the database checks
your work.**

**5. Nothing in the file is destructive until the last statement.** Run `Up` and stop it before the
`DropColumn`, and `catalog_sets` has both `theme` and `theme_id`, agreeing with each other. That is
what makes this migration safe to run against a database somebody is using, and it is the general
shape of the expand/contract pattern the deployment episodes will name properly.

### Run it, both ways, on camera

```bash
dotnet ef database update --project src/Catalog/BrickShare.Catalog.Api
```

```sql
select t.name, count(*) from catalog_sets s join themes t on t.id = s.theme_id group by t.name;

\d catalog_sets
```

Then go back, which is the half nobody demonstrates:

```bash
dotnet ef database update AddRebrickableSnapshots --project src/Catalog/BrickShare.Catalog.Api
```

```sql
select set_number, theme from catalog_sets;
```

The names are back. **A `Down` is not paperwork; it is the rollback that happens at 2 a.m. when the
deploy goes wrong**, and the only time to find out it loses data is now. Then run `update` again to
land forwards, and move on.

---

## Step 7 — The test that can finally fail for the right reason

The integration suite went red in step 4 for a reason that had nothing to say about anybody's code,
and green again the moment step 6's migration landed. **Run it now, before writing anything**: the
whole suite passes, against a schema that did not exist twenty minutes ago.

Which leaves the gap this step fills. Nothing yet proves the *point* of the episode — that two sets
from one theme produce **one** theme row. The handler in step 5 was driven by the compiler, and a
compiler will happily accept an `AdoptThemeAsync` that inserts a duplicate every time.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/ThemeTests.cs — new file
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.CatalogSets;

public class ThemeTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task Two_sets_from_the_same_theme_share_one_theme_row()
    {
        HttpClient client = Database.Api.CreateClient();

        await Database.CatalogueTitanicAsync(client);
        await CatalogueConcordeAsync(client);

        await using CatalogDbContext dbContext = Database.NewDbContext();

        Assert.Equal(2, await dbContext.Sets.CountAsync());
        Assert.Equal(1, await dbContext.Themes.CountAsync());

        Assert.Equal(1, await dbContext.Sets
            .Select(set => set.ThemeId)
            .Distinct()
            .CountAsync());
    }

    [Fact]
    public async Task A_catalogued_set_still_reports_its_theme_by_name()
    {
        HttpClient client = Database.Api.CreateClient();

        Guid lookupId = await Database.LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets",
            new
            {
                lookupId,
                retailPrice = 629.99m,
                baseRentalPrice = 60.00m,
                minimumRentalDays = 7,
                minimumAge = 18
            });

        CatalogSetResponse? created = await response.Content.ReadFromJsonAsync<CatalogSetResponse>();

        Assert.NotNull(created);
        Assert.Equal("Icons", created.Theme);
    }

    /// <summary>
    /// A second Icons set. The theme id is the same 252 the Titanic came back with, which is the
    /// only thing that makes the first test meaningful.
    /// </summary>
    private async Task CatalogueConcordeAsync(HttpClient client)
    {
        Database.Rebrickable.Sets["10318-1"] = new
        {
            set_num = "10318-1",
            name = "Concorde",
            year = 2023,
            theme_id = 252,
            num_parts = 2083,
            set_img_url = "https://cdn.rebrickable.com/media/sets/10318-1.jpg"
        };

        Database.Rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)null };

        HttpResponseMessage lookup = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10318-1" });

        LookupResponse? draft = await lookup.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets",
            new
            {
                lookupId = draft.LookupId,
                retailPrice = 199.99m,
                baseRentalPrice = 25.00m,
                minimumRentalDays = 7,
                minimumAge = 12
            });

        response.EnsureSuccessStatusCode();
    }
}
```

The first test is the episode in one assertion: **two sets, two lookups, two snapshots — and one
theme row.**

**Be honest about what colour it is when it lands.** Against the handler written in step 5 it is
green immediately, because the behaviour it describes is already there — and `CLAUDE.md`'s rule
covers exactly this case: *some tests drive a design and some describe a rule; both belong in the
suite, and only the first is TDD doing its job.* This one describes a rule.

To see it red, comment out the `FirstOrDefaultAsync` lookup at the top of `AdoptThemeAsync` and run
it again: two theme rows, the assertion fails, and the unique index does not save you because the
two requests are sequential. **Ten seconds, and it is the difference between a test you believe and
a test you assume.**

The second test costs nothing and guards the wire contract: the response still says
`"theme": "Icons"`, and the day somebody changes that, this fails rather than a client does.

> **End of 33a.** `dotnet build` clean, `dotnet test` green, the schema changed and the data intact.
> If you are recording two episodes, close here on the `select … join themes` output and the
> sentence: *the catalog now has a list of themes instead of a habit of spelling them.*

---

# 33b — Which theme is it really?

## Step 8 — The tree upstream, and the shop that does not have one

No code in this step. Open `src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableThemePayload.cs`
and read the field episode 27 mapped and never used:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableThemePayload.cs — already there
    [property: JsonPropertyName("parent_id")]
    int? ParentId);
```

Then call Rebrickable on camera for a set that is not top-level — a Star Wars UCS set is the easiest
example — and follow the ids by hand:

```
GET /api/v3/lego/sets/75192-1/     → theme_id: 171
GET /api/v3/lego/themes/171/       → "Ultimate Collector Series", parent_id: 158
GET /api/v3/lego/themes/158/       → "Star Wars", parent_id: null
```

The Titanic answers 252 → *Icons*, `parent_id: null`, one level, which is why nothing has noticed
this for six episodes. **The shop's own catalog has been storing whichever node Rebrickable happened
to attach the set to, with no rule about what level that is.**

Three options, and this is a product decision. Ask what a customer does with it.

| | Store the leaf | Store the root | Store both |
| --- | --- | --- | --- |
| The filter list in UC-7.2 | Hundreds of entries, many meaningless alone — *Episode IV*, *Accessories* | Twenty-ish entries, every one a thing a person would say | Two filters, one of which needs the other to make sense |
| "Show me Star Wars sets" | Misses every set filed under a sub-theme | Works | Works |
| Precision lost | None | The sub-theme, recoverable from Rebrickable by set number at any time | None |
| Cost | Nothing | Up to two extra calls per lookup | A second column, a second table, a hierarchy in the UI |

**Store the root.** The deciding row is the first one: the point of this whole episode is to produce
a filter list a customer can read, and *Episode IV* is not a thing anybody browses a rental shop by.
The second row is the one that would actually generate complaints.

**And the third row is why it is safe.** Storing the root discards the sub-theme name, and the honest
version of that sentence is *discards it from the catalog, where nothing uses it*. The set number is
still there; Rebrickable still answers; and the day the shop wants sub-themes, this episode's `themes`
table already carries `rebrickable_id`, which is the key needed to go and ask. **Both-is-better looks
free and is not** — it is a parent column, a recursive query, and a two-level UI, in a shop with a
few hundred sets.

**Caveman version:** stranger keep box in box in box in box. Customer not care. Customer say "show
Star Wars". Shop must know which pile is Star Wars pile. So: walk up from small box to big box, write
down big box name. Small box name still with stranger if shop ever want.

> **The honest wrinkle, said out loud and not fixed here.** Sets catalogued before this episode were
> backfilled in step 6 from snapshots that recorded whatever level Rebrickable named. After this step
> new lookups record roots. So for a short while the `themes` table can hold a leaf and a root side by
> side. The fix is a one-off reconciliation — walk each theme's `rebrickable_id` up and merge — and
> it is a job, not an endpoint. With a handful of dev rows today, it is not worth a script; the day
> there is production data it is worth a real one, written against real rows.

---

## Step 9 — Red: the lookup walks up

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/LookupTests.cs — add above the private Titanic helper
    [Fact]
    public async Task A_nested_theme_is_stored_as_the_one_customers_browse_by()
    {
        Database.Rebrickable.Sets["75192-1"] = MillenniumFalcon();
        Database.Rebrickable.Themes[171] =
            new { id = 171, name = "Ultimate Collector Series", parent_id = (int?)158 };
        Database.Rebrickable.Themes[158] = new { id = 158, name = "Star Wars", parent_id = (int?)null };

        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "75192-1" });

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);
        Assert.Equal("Star Wars", draft.Theme);
    }

    [Fact]
    public async Task A_top_level_theme_costs_one_call_and_not_two()
    {
        Database.Rebrickable.Sets["10294-1"] = Titanic();
        Database.Rebrickable.Themes[252] = Icons();

        HttpClient client = Database.Api.CreateClient();

        await client.PostAsJsonAsync("/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        // One for the set, one for its theme. A parentless theme must not provoke a third.
        Assert.Equal(2, Database.Rebrickable.Requests);
    }

    [Fact]
    public async Task A_cycle_in_the_theme_tree_does_not_hang_the_lookup()
    {
        Database.Rebrickable.Sets["10294-1"] = Titanic();
        Database.Rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)900 };
        Database.Rebrickable.Themes[900] = new { id = 900, name = "Loop", parent_id = (int?)252 };

        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static object MillenniumFalcon() => new
    {
        set_num = "75192-1",
        name = "Millennium Falcon",
        year = 2017,
        theme_id = 171,
        num_parts = 7541,
        set_img_url = "https://cdn.rebrickable.com/media/sets/75192-1.jpg"
    };
```

The first one is red — `draft.Theme` comes back *Ultimate Collector Series*. **The second one passes
the moment it is written, and keep it anyway**: it describes a rule the fix could easily break — a
walk that always fetches the parent would make it three — and a test that guards against a
regression somebody has not made yet is still doing its job. The third is red in the worst way a
test can be: it does not fail, it hangs, until the walk has a bound.

Green is one mapped field and one method.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableTheme.cs — replaces the record
namespace BrickShare.Catalog.Api.Rebrickable;

public sealed record RebrickableTheme(int Id, string Name, int? ParentId);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableClient.cs — the last line of FindThemeAsync
        return new RebrickableTheme(payload.Id, payload.Name, payload.ParentId);
```

`RebrickableClientTests` keeps compiling: it asserts on `Name` and never constructed the record by
hand. **That is the payoff for episode 27's two-types-for-one-concept rule showing up a second time**
— the payload changed shape upstream and our record changed shape here, independently, six episodes
apart.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — what is there now
        RebrickableTheme? theme = await rebrickable.FindThemeAsync(set.ThemeId, cancellationToken);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — replaces that line
        RebrickableTheme? theme = await FindBrowsableThemeAsync(
            rebrickable, set.ThemeId, cancellationToken);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — add below LookUpAsync
    /// <summary>
    /// Rebrickable nests themes; BrickShare stores the top of the tree, because that is the level
    /// customers browse by. See episode 33, step 8 — a product decision, not a mapping one.
    /// </summary>
    private static async Task<RebrickableTheme?> FindBrowsableThemeAsync(
        IRebrickableCatalog rebrickable,
        int themeId,
        CancellationToken cancellationToken)
    {
        RebrickableTheme? theme = await rebrickable.FindThemeAsync(themeId, cancellationToken);

        for (int step = 0; theme?.ParentId is int parentId && step < MaximumThemeDepth; step++)
        {
            RebrickableTheme? parent = await rebrickable.FindThemeAsync(parentId, cancellationToken);

            // A parent id that resolves to nothing is Rebrickable contradicting itself. The theme
            // we already have is a real theme, so keep it rather than failing the whole lookup.
            if (parent is null)
            {
                return theme;
            }

            theme = parent;
        }

        return theme;
    }
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — add above MapCatalogLookups
    /// <summary>
    /// Rebrickable's tree is three levels deep. Ten is room to spare and a hard stop if the data
    /// ever contains a cycle: a walk up a graph somebody else owns needs a bound, not a promise.
    /// </summary>
    private const int MaximumThemeDepth = 10;
```

### The three decisions inside twelve lines

**The bound is not about depth, it is about trust.** Rebrickable's themes are three levels at most,
so the loop will not come close to ten. The guard exists because the data is not ours: a cycle in
someone else's table becomes an infinite loop in our process, holding a request thread and an HTTP
connection until something times out. **Every walk over remote data gets a counter.** Not because the
remote data is bad, but because we cannot prove it is good.

**The loop stops quietly rather than throwing.** Hitting ten means the upstream tree is nonsense, and
the alternatives were a `502` — refuse the lookup, staff cannot catalogue the set — or store the
deepest theme resolved, which is a real theme with a real name. The second is chosen because it
degrades: a slightly-too-specific theme is a bad filter entry, and a refused lookup is a set the shop
cannot rent out. **Prefer the failure mode that keeps the shop trading.** Close call, and the other
answer is defensible if you would rather see the problem than absorb it.

**Up to three calls where there was one**, and nothing caches them. Cataloguing is human-paced and
rare — episode 27 made that argument about caching lookups, and it holds harder here. Say the number
out loud, because "it is only a few more calls" is how a sub-millisecond loop becomes a
sub-second one.

**Caveman version:** climb up tree. Maybe tree is not tree, maybe tree is circle — then climb
forever, never come back. So: count step. Ten step, stop, keep branch have in hand. Better small
wrong answer than hunter who never return.

---

## Step 10 — The requests, and the proof

**Not driven by a test.** A `.http` file is a request collection; the behaviour is already pinned by
the tests in steps 7 and 9.

```
// src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.http — appended at the end
### Look up a nested theme. Rebrickable says Ultimate Collector Series; we store Star Wars.
POST {{host}}/api/v1/catalog/lookups
Content-Type: application/json

{ "setNumber": "75192-1" }

### Catalogue it. The response says "theme": "Star Wars" — a name, exactly as before.
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{
  "lookupId": "paste-the-lookupId-from-above",
  "retailPrice": 849.99,
  "baseRentalPrice": 80.00,
  "minimumRentalDays": 7,
  "minimumAge": 16
}

### A second set from the same theme. One more row in catalog_sets, none in themes.
POST {{host}}/api/v1/catalog/lookups
Content-Type: application/json

{ "setNumber": "75375-1" }
```

Run the first two against a real key, then go to `psql` and show the thing the whole episode was
for:

```sql
select t.name as theme, count(s.id) as sets
from themes t
left join catalog_sets s on s.theme_id = t.id
group by t.name
order by t.name;
```

**That result set is episode 34's filter list**, produced by a query with no `distinct` in it, over a
column nothing can misspell. Say that, then stop.

---

## What this episode is not

**No `GET /catalog/themes`.** The table exists and nothing serves it. That endpoint belongs with the
filters that use it — **episode 34** — and shipping it here would mean a route with no caller and no
test that means anything.

**No sub-themes.** Decided against in step 8, with the reasoning on screen, and the upstream ids kept
so the decision is reversible. **A decision you can reverse cheaply is worth making early; one you
cannot is worth deferring.**

**No reconciliation of the themes backfilled in step 6.** Named in step 8's wrinkle, deliberately not
built. It is a job, and this service has no jobs; UC-11's scheduled work is a Functions episode a
long way after this module.

**No rename handling.** If Rebrickable renames a theme tomorrow, `themes.name` keeps yesterday's
spelling until somebody updates it — because `AdoptThemeAsync` looks the theme up by
`rebrickable_id`, finds it, and returns it without looking at the name. **That is the correct
behaviour for a write path** — a catalogue request is not the place to discover an upstream edit —
and the right place for the fix is the same job as the paragraph above.

**No theme on the copy.** Copies belong to sets, sets belong to themes, and a copy's theme is one
join away. Denormalising it onto `copies` would be a second place for the same fact to be wrong.

**No caching of theme lookups**, for episode 27's reason, now costing up to three calls instead of
one. If cataloguing ever becomes a bulk import — a hundred sets at a time — that arithmetic changes
and a per-request memo is worth ten lines. It is not a hundred sets at a time.

**No authorization.** These endpoints are still open to anyone who can reach the service, exactly as
they were in episode 27. **Episode 40.**

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green — including the three new unit tests, two new theme tests and three lookup tests |
| `git stash` the step 5 endpoint fix, then `dotnet ef migrations add Scratch` | **Fails to build.** Worth doing once: it is the whole reason step 5 precedes step 6 |
| `dotnet ef migrations list` | `AddThemes` last |
| `dotnet ef migrations add Scratch`, then `remove` | An **empty** `Up` — the hand-edited file and the model still agree |
| `\d themes` in psql | Three columns, unique index on `rebrickable_id`, plain index on `name` |
| `\d catalog_sets` | `theme_id uuid not null`, `fk_catalog_sets_theme_id`, and **no** `theme` column |
| `select count(*) from catalog_sets where theme_id is null` | `0` — and it could not be otherwise; the constraint says so |
| `database update AddRebrickableSnapshots`, then `select theme from catalog_sets` | The names are back. The `Down` restores data, not just shape |
| Catalogue two sets from one theme | `select count(*) from themes` → `1` |
| Lookup of `75192-1` | `"theme": "Star Wars"`, not `"Ultimate Collector Series"` |
| `jq '.components.schemas.CatalogSetResponse.properties.theme'` | `string` — unchanged. The contract did not move |
| `git diff --stat src/Catalog/BrickShare.Catalog.Domain` | Two files: a new `Theme.cs` and a changed `CatalogSet.cs`. No EF anywhere in either |

The row to actually run on camera is the fourth. **A hand-edited migration is the one place in this
codebase where the tooling can no longer check the work** — except that it still can, once, by being
asked whether anything is left to generate. An empty `Scratch` migration is the proof that the file
you rewrote still describes the model everyone else compiles against.

## Next

[Episode 34 — Browse and filter](episode-34.md): the public catalog listing, with the filter
this episode made possible. The themes list becomes a real endpoint, the grade multipliers get a
table of their own, and a view computes the one number a customer browses by that no table
stores: the starting price.

The sentence to carry out of this one: **a schema change and a contract change are different
events, and the day you cannot tell them apart is the day you stop being able to fix your own
database.** `catalog_sets` lost a column, gained a foreign key, and every client on the internet saw
exactly the same JSON before and after.
