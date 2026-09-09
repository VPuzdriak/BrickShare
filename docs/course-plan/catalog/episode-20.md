# Episode 20 — The set the copies are copies of

← [Course plan](catalog-api.md) · Previous: [Episode 19 — Migrations in the pipeline](episode-19.md)

`Copy` has existed since episode 13, and **the thing it is a copy of has never been modelled**.
Episodes 13 to 15 built the box — its label, its grade, its eleven state transitions — and never the
LEGO set inside it. The gap has been invisible because nothing could ask for it.

This is the first of five episodes building the staff endpoint that catalogues a set, and the only
one with no HTTP in it: the product first, then the door.

**Done when** a `catalog_sets` table exists in Compose and in Azure, created by a migration the
pipeline applied, with money stored as `numeric(10,2)`, the set number unique in the **database**,
and a domain type that refuses to describe a set the shop could never rent out.

## Before recording

- Episode 19 merged: the migrate job green, `copies` and `__EFMigrationsHistory` in the Azure
  database, `deploy` gated behind them.
- Docker running.
- A branch.
- [`docs/IDEA.md`](../../IDEA.md) open at **UC-1.1** and at **UC-2's business rules**. The 28-day
  number in step 1 comes from the second of those, and the episode is much less convincing if it
  has to be taken on trust.
- [`docs/architecture/catalog.md`](../../architecture/catalog.md) open at *Schema*.

**Step 1 is test-first; step 2 is not, and says so.** A domain type with a rule in it is exactly
what TDD is for. An EF configuration and a generated migration are what `CLAUDE.md` exempts —
mapping is configuration, and a test asserting `ToTable("catalog_sets")` would be a transcription of
the line above it rather than a check on it.

## Where these five episodes are going

Worth thirty seconds up front, because the block only makes sense as a shape:

```
   request ──▶ [ edge validation ]──▶ [ domain rules ]──▶ [ database constraints ]
                      │                     │                      │
                     400                   409                    409
              "could anybody         "does this shop        "has somebody
               mean this?"            allow it?"             already done it?"
```

Three gates, three different reasons to refuse, and **none of them is redundant**. Episode 22 builds
the first, episode 23 the second, episode 24 the third. Today builds the thing all three are
guarding, and episode 21 builds the door they sit behind.

---

## Step 0 — What UC-1.1 actually asks for

Read it out, because these five episodes deliberately build only half of it:

> **UC-1.1 — Catalogue a new set.** Staff type a LEGO set number. BrickShare looks it up on
> Rebrickable and **prefills the form** with name, year, theme, piece count, image and the set's
> **minifigures**. Staff then type the four fields Rebrickable cannot supply: **retail price**,
> **base rental price**, **minimum rental duration** and **age rating**.

The lookup, the snapshot, the image copy and the checklist are episodes 25 and 26. Episodes 20 to 24
build **the set and the create call**, taking every field — product facts and staff-typed four
alike.

**Say now that this is temporary and why**, because otherwise the next four episodes teach something
episode 26 spends its whole runtime undoing:

> Accepting `name`, `theme` and `pieceCount` from the client means anyone who can call this endpoint
> can invent a LEGO set that does not exist. That is an authorization bug wearing the costume of an
> API design choice, and it is episode 26's whole subject. It is built the wrong way first because
> the fix is only convincing once the wrong version is on screen and obviously reasonable.

**This step is not code.**

---

## Step 1 — The set, driven out by its rules

Start where episode 12 started — with the rule that is not obvious.

`tests/BrickShare.Catalog.UnitTests/CatalogSetTests.cs`:

```csharp
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CatalogSetTests
{
    [Fact]
    public void A_set_cannot_require_a_rental_longer_than_the_shop_can_recover_it_in()
    {
        Assert.Throws<InvalidOperationException>(() => Catalogue(minimumRentalDays: 29));
    }
}
```

```
error CS0103: The name 'Catalogue' does not exist in the current context
```

A build error against a type that does not exist is a legitimate red — `CLAUDE.md` says so, and this
is the plainest example of it in the module.

### Why 29 is the interesting number

Twenty-nine is not a typo and it is not a constant somebody picked. `docs/IDEA.md` fixes the maximum
rental at 28 days for one reason, written down in UC-2:

> The hold lasts 30 days and the rental caps at 28 precisely so that this is guaranteed — an expired
> authorization cannot be captured, and waiting for the boundary would risk finding nothing there.

A Stripe deposit authorization dies at 30 days. The write-off job fires on day 28 while the money is
still reachable. So **a set whose minimum rental is 29 days is a set the shop can never legally get
paid for if the customer keeps it** — the write-off would fire before the rental period the customer
was sold had even ended.

That is a rule about money in another service, enforced here, in the type, on the day the set is
catalogued. Which is worth saying out loud: **the rule lives where it can be enforced cheaply, not
where it originates.** Catalog does not know what Stripe is and never will.

### Green

`src/Catalog/BrickShare.Catalog.Domain/CatalogSet.cs`:

```csharp
namespace BrickShare.Catalog.Domain;

/// <summary>
/// One LEGO set the shop catalogues. The shop may own three Titanics: this is the single
/// description they share, and <see cref="Copy"/> is one of the three boxes.
/// </summary>
public sealed class CatalogSet
{
    /// <summary>
    /// No rental may be longer than this. UC-2's write-off fires on day 28, while the deposit
    /// authorization — which dies at 30 — is still capturable. A minimum longer than the maximum
    /// would be a set the shop sells and cannot recover.
    /// </summary>
    public const int MaximumRentalDays = 28;

    private CatalogSet(
        SetNumber number,
        string name,
        string theme,
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
        Theme = theme;
        Year = year;
        PieceCount = pieceCount;
        RetailPrice = retailPrice;
        BaseRentalPrice = baseRentalPrice;
        MinimumRentalDays = minimumRentalDays;
        MinimumAge = minimumAge;
    }

    public Guid Id { get; }

    public SetNumber Number { get; }

    public string Name { get; }

    public string Theme { get; }

    public int Year { get; }

    public int PieceCount { get; }

    public Money RetailPrice { get; }

    public Money BaseRentalPrice { get; }

    public int MinimumRentalDays { get; }

    public int MinimumAge { get; }

    public static CatalogSet Catalogue(
        SetNumber number,
        string name,
        string theme,
        int year,
        int pieceCount,
        Money retailPrice,
        Money baseRentalPrice,
        int minimumRentalDays,
        int minimumAge)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(theme);
        ArgumentOutOfRangeException.ThrowIfLessThan(pieceCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(retailPrice.Amount);
        ArgumentOutOfRangeException.ThrowIfNegative(baseRentalPrice.Amount);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumAge);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1);

        if (minimumRentalDays > MaximumRentalDays)
        {
            throw new InvalidOperationException(
                $"A set cannot require {minimumRentalDays} days. The shop rents for at most "
                + $"{MaximumRentalDays} days, so a longer minimum could never be met.");
        }

        return new CatalogSet(
            number, name, theme, year, pieceCount,
            retailPrice, baseRentalPrice, minimumRentalDays, minimumAge);
    }
}
```

Green. Now fill in the rest of the specification:

```csharp
    [Fact]
    public void A_catalogued_set_keeps_the_facts_it_was_given()
    {
        CatalogSet set = Catalogue();

        Assert.Equal(SetNumber.Parse("10294-1"), set.Number);
        Assert.Equal("Titanic", set.Name);
        Assert.Equal(new Money(629.99m), set.RetailPrice);
        Assert.Equal(7, set.MinimumRentalDays);
        Assert.NotEqual(Guid.Empty, set.Id);
    }

    [Fact]
    public void A_set_may_be_catalogued_at_exactly_the_maximum_rental_period()
    {
        CatalogSet set = Catalogue(minimumRentalDays: CatalogSet.MaximumRentalDays);

        Assert.Equal(28, set.MinimumRentalDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_set_must_be_rentable_for_at_least_one_day(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Catalogue(minimumRentalDays: days));
    }

    [Fact]
    public void A_set_cannot_be_catalogued_at_a_negative_retail_price()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Catalogue(retailPrice: new Money(-1m)));
    }

    private static CatalogSet Catalogue(
        int minimumRentalDays = 7,
        Money? retailPrice = null) =>
        CatalogSet.Catalogue(
            SetNumber.Parse("10294-1"),
            "Titanic",
            "Icons",
            2021,
            9090,
            retailPrice ?? new Money(629.99m),
            new Money(60.00m),
            minimumRentalDays,
            18);
```

**Say this out loud, because episode 12 promised it would come up:** four of those five tests passed
the instant they were written. They did not drive a design. They *describe a rule*, and a rule
nobody can accidentally delete is worth more than a rule somebody remembers. Both kinds belong in
the suite; only the first kind is TDD doing its job, and pretending otherwise is how TDD gets a
reputation for ceremony.

### Two shapes worth pointing at

**The boundary at exactly 28 has its own test.** `>` versus `>=` in that comparison is one
keystroke, and off-by-one at a boundary is the most common arithmetic bug in business rules.
`..._at_exactly_the_maximum_rental_period` is the test that fails if somebody tightens it.

**Validation is in the factory, not the constructor**, and that is load-bearing in step 2. EF Core
materialises entities by calling a constructor with values read out of the database. If the rules
lived there, loading a row catalogued under a 28-day policy would explode the day the business
changed that policy to 21. **Rules apply when a thing is created, not every time it is remembered.**
`Copy.Register` has had this shape since episode 13 for the same reason.

`Id` is assigned in the private constructor and has no setter. EF writes it through the backing
field after construction, which episode 16 established and `CopyPersistenceTests` proves on every
run.

---

## Step 2 — A table for it

`src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogSetConfiguration.cs`, which deliberately
looks like `CopyConfiguration.cs` because there is nothing new to say:

```csharp
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class CatalogSetConfiguration : IEntityTypeConfiguration<CatalogSet>
{
    public void Configure(EntityTypeBuilder<CatalogSet> builder)
    {
        builder.ToTable("catalog_sets");

        builder.HasKey(set => set.Id);
        builder.Property(set => set.Id).HasColumnName("id");

        builder.Property(set => set.Number)
            .HasColumnName("set_number")
            .HasConversion(number => number.Value, value => SetNumber.Parse(value))
            .HasMaxLength(32)
            .IsRequired();

        // The invariant, in the database. Episode 16 made the same argument for label codes:
        // an application check is a race, and a unique index is an arbiter. Episode 24 uses it.
        builder.HasIndex(set => set.Number)
            .IsUnique()
            .HasDatabaseName("ix_catalog_sets_set_number");

        builder.Property(set => set.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(set => set.Theme)
            .HasColumnName("theme")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(set => set.Year).HasColumnName("year").IsRequired();
        builder.Property(set => set.PieceCount).HasColumnName("piece_count").IsRequired();

        // No HasColumnType on the two money columns. CatalogDbContext.ConfigureConventions
        // already says every Money in this model is numeric(10,2), and episode 16 put it there
        // precisely so this file cannot get it wrong by forgetting.
        builder.Property(set => set.RetailPrice).HasColumnName("retail_price").IsRequired();
        builder.Property(set => set.BaseRentalPrice).HasColumnName("base_rental_price").IsRequired();

        builder.Property(set => set.MinimumRentalDays).HasColumnName("minimum_rental_days").IsRequired();
        builder.Property(set => set.MinimumAge).HasColumnName("minimum_age").IsRequired();

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
```

`CatalogDbContext` gains one line:

```csharp
    public DbSet<CatalogSet> Sets => Set<CatalogSet>();
```

and nothing else — `ApplyConfigurationsFromAssembly` finds the new configuration class without being
told, which is the payoff for the line episode 16 wrote instead of registering each one by hand.

**The convention worth pausing on for ten seconds** is the one that did nothing visible: two money
columns just became `numeric(10,2)` because a rule written four episodes ago applied to a type that
did not exist yet. That is the difference between a convention and a checklist.

```bash
dotnet ef migrations add AddCatalogSets --project src/Catalog/BrickShare.Catalog.Api
```

```csharp
migrationBuilder.CreateTable(
    name: "catalog_sets",
    columns: table => new
    {
        id = table.Column<Guid>(type: "uuid", nullable: false),
        set_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
        name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
        theme = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
        year = table.Column<int>(type: "integer", nullable: false),
        piece_count = table.Column<int>(type: "integer", nullable: false),
        retail_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
        base_rental_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
        minimum_rental_days = table.Column<int>(type: "integer", nullable: false),
        minimum_age = table.Column<int>(type: "integer", nullable: false)
    },
    constraints: table => table.PrimaryKey("PK_catalog_sets", x => x.id));

migrationBuilder.CreateIndex(
    name: "ix_catalog_sets_set_number",
    table: "catalog_sets",
    column: "set_number",
    unique: true);
```

**Read the two `numeric(10,2)` lines on camera.** Episode 13 argued about `decimal` versus `double`
in C#; this is the same argument surviving the trip into Postgres, and it is the line that would
have been `double precision` if the convention had not been there.

Then the check that matters more than reading it:

```bash
dotnet test    # green — every test replays this migration into a fresh container
```

Episode 17 built a fixture that migrates a throwaway Postgres from empty on every run, so a
migration that does not apply cleanly cannot reach a commit. **Nothing was configured to make that
true today. It was already true**, which is the return on an episode that looked like plumbing.

Push it, and the pipeline applies the same migration to Azure before the new revision goes live —
episode 19 doing its job for the first time on a schema change it did not know about.

---

## What this episode is not

**No endpoint.** Nothing can create a `CatalogSet` except a test. That is episode 21, and the
separation is deliberate: the type and its rules are worth understanding before there is an HTTP
status code to argue about.

**No `set_id` on `copies`.** A copy plainly belongs to a set, and the foreign key is not here. It
arrives in episode 27, with the endpoint that registers copies *against* a set — the first moment
anything can write it. A nullable column with nothing populating it lies for seven episodes, and
every read written in the meantime has to handle a case that exists only because it shipped early.

**No checklist table.** `catalog_set_checklist_items` is seeded from the minifigures a Rebrickable
lookup returns, and `source = 'rebrickable' | 'manual'` is a column whose whole point is that
distinction. Building it now means building it with only the manual half of its story available.
Episode 26 has both halves.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings — the new files pass episode 10's `TreatWarningsAsErrors` |
| `dotnet test` | Green, including the four new `CatalogSetTests` |
| `CatalogSet.Catalogue(minimumRentalDays: 29)` | `InvalidOperationException` quoting the 28-day rule |
| `CatalogSet.Catalogue(minimumRentalDays: 28)` | Succeeds — the boundary is inclusive |
| `psql` → `\d catalog_sets` | `retail_price` and `base_rental_price` are `numeric(10,2)` |
| `psql` → `\d catalog_sets` | `ix_catalog_sets_set_number` present and unique |
| Insert the same `set_number` twice by hand | `23505: duplicate key value violates unique constraint` |
| The pipeline, after a push | `Applying migration '…_AddCatalogSets'.`, then `deploy` |
| `terraform plan` | `No changes.` — nothing here is infrastructure |

## Next

[Episode 21 — The first endpoint](episode-21.md):
there is now a table with nothing that can write to it, which is the same sentence episode 19 ended
with, one layer up.

Episode 21 opens the door: route groups, `/api/v1` from the very first public endpoint rather than
when it hurts, a handler that turns a request body into a `CatalogSet`, and a `201 Created` that
comes back from Azure. It also ends with the least comfortable `curl` in the module — the same
request, from anywhere on the internet, with no token.
