# Episode 29 — A copy belongs to a set

← [Course plan](catalog-api.md) · Previous: [Episode 28 — The request body that loses its facts](episode-28.md)

Episodes 20 to 28 built one thing: the description of a LEGO set, with facts the client cannot
invent. What the shop actually rents is not a description. It is a cardboard box on a shelf, and
there may be three of them behind the same description.

`Copy` has existed since episode 15 — a state machine with a grade, a status and eight legal
transitions, every rule of it unit tested. It has never been reachable over HTTP, and it is missing
the two things a real box needs:

```csharp
// src/Catalog/BrickShare.Catalog.Domain/Copy.cs — as it is
    public static Copy Register(LabelCode label, ConditionGrade startingGrade)
```

**Which set is this a copy of?** Nothing says. **What did it weigh when it was known complete?**
Nothing says that either, and `docs/IDEA.md` has called baseline weight the fastest way to notice a
missing bag of pieces since before there was any code.

This episode fixes both, mints the label BrickShare sticks on the box, and puts one endpoint in
front of it: register **one** copy against **one** set.

**Done when** `POST /api/v1/catalog/sets/{setId}/copies` returns `201` with a label code the client
never chose, a set id that has to exist, and a weight somebody actually put on a scale.

> **Runtime: about 16 minutes.** If it runs long, **step 6** is the cut — the mint-collision retry
> makes the same point again in episode 30, where a batch mints several labels at once.

## Before recording

- Episode 28 merged: `POST /catalog/sets` takes a `lookupId`, and `CatalogueSetTests` is green.
- `docker compose up` for Postgres, and `dotnet user-secrets` still holding your Rebrickable key.
- A branch, and `dotnet ef` on the path — **this episode does add a migration**, unlike the last one.
- [`docs/architecture/catalog.md`](../../architecture/catalog.md) open at **Open questions**. Step 1
  closes one of them on camera, and it reads better with the question visible.

**Two of the seven steps are not driven by a test, and both say so where they appear**: step 4 is
persistence mapping and a generated migration, and step 7 is a request collection. Everything else
goes red first.

Every sample below names its file and where in it the code goes. Where something is edited in a
file that already exists, the **first block is what is already there** — the anchor to find on
screen — and the **second block is what to paste**. Every block is copy-paste clean: no markers,
no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `src/…/Domain/Copy.cs` | `Register` grows two arguments; two new properties |
| `src/…/Domain/LabelCode.cs` | The alphabet becomes a `const`; `Mint()` |
| `tests/…/UnitTests/ACopy.cs` | **New** — the helper that keeps thirteen call sites out of this change |
| `tests/…/UnitTests/CopyIdentityTests.cs` | Rewritten — three tests kept, three added |
| `tests/…/UnitTests/CopyGradeTests.cs`, `CopyStatusTests.cs` | One find-and-replace, thirteen call sites |
| `tests/…/UnitTests/LabelCodeTests.cs` | Three tests for minting |
| `src/…/Api/Persistence/CopyConfiguration.cs` | `catalog_set_id`, the foreign key, `baseline_weight_grams` |
| `src/…/Api/Migrations/…_AddCopySetAndBaselineWeight.cs` | **Generated** |
| `tests/…/IntegrationTests/Persistence/CopyPersistenceTests.cs` | A copy now needs a set to belong to |
| `tests/…/IntegrationTests/CatalogFlow.cs` | **New** — episode 28's private helper, shared |
| `tests/…/IntegrationTests/CatalogSets/CatalogueSetTests.cs` | Points at the shared helper |
| `tests/…/IntegrationTests/Copies/RegisterCopyTests.cs` | **New** — four tests |
| `tests/…/IntegrationTests/CatalogApiFactory.cs` | One property: the wire format, read off the host |
| `src/…/Api/Endpoints/CopyEndpoints.cs` | **New** |
| `src/…/Api/Endpoints/RegisterCopyRequestValidator.cs` | **New** |
| `src/…/Api/Program.cs` | One converter, one validator, one `MapCopies()` |
| `src/…/Api/BrickShare.Catalog.Api.http` | Four requests |

---

## Step 1 — The box on the shelf has no set

Open `Copy.cs` and the very first migration side by side:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Migrations/20260821222411_InitialCatalog.cs — as it is
            migrationBuilder.CreateTable(
                name: "copies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    grade = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
```

Five columns, and **none of them says which set this is a copy of**. That was not an oversight in
episode 16: there was no `catalog_sets` table to point at yet, and a foreign key to a table that
does not exist is not a thing you can write. Episode 20 built that table. The link goes on now.

The second gap is the one worth arguing about, because `docs/architecture/catalog.md` lists it as an
open question:

> **Batch registration and baseline weight.** UC-1.2 allows "we bought 3" as one action, but
> baseline weight is per copy and has to be weighed individually. Whether the batch creates three
> copies pending weights, or the weights are required up front, is a workflow question.

**This course answers: required up front.** A copy is registered with a weight or it is not
registered. The three consequences, said out loud, because this is a judgement call and pretending
otherwise teaches worse than admitting it:

| | Required up front (chosen) | Nullable, recorded later |
| --- | --- | --- |
| The column | `int`, not null. A copy row is never half a copy | Nullable, and every reader has to handle "unknown" |
| The rule nobody wants to write | — | *Can a copy with no baseline weight be rented?* Whatever you answer, you now own it |
| The cost | Staff weigh before they type. Real, and it is a counter-top scale, not a ceremony | None, and the debt lands on the returns flow in a later module |

The deciding argument is the second row. A nullable weight does not remove the work, it moves it
into the one place where the weight is *used* — the return — and turns "this box is 40 grams light"
into "this box has no baseline, so we cannot tell". **The failure mode of asking too early is an
annoyed staff member; the failure mode of asking too late is a missing bag nobody noticed.**

And a note for the honest column: the shop that bought four boxes will call this endpoint four
times, because that is all it does today. Episode 30 fixes that, and it fixes it by changing this
endpoint rather than adding a second one beside it.

---

## Step 2 — Red: `Register` grows two arguments

The domain first, and the red is a build error — which `CLAUDE.md` counts as a legitimate red, since
a test that will not compile against a type that does not exist yet has still failed for the right
reason.

```csharp
// tests/BrickShare.Catalog.UnitTests/CopyIdentityTests.cs — replace the whole file
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyIdentityTests
{
    [Fact]
    public void Two_registered_copies_have_different_identities()
    {
        Copy first = ACopy.Graded(ConditionGrade.New);
        Copy second = ACopy.Graded(ConditionGrade.New);

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void A_copy_on_the_shelf_has_no_retirement_date()
    {
        Copy copy = ACopy.Graded(ConditionGrade.New);

        Assert.Null(copy.RetiredAt);
    }

    [Fact]
    public void Retiring_a_copy_records_when_it_happened()
    {
        Copy copy = ACopy.Graded(ConditionGrade.New);
        DateTimeOffset when = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

        copy.Retire(when);

        Assert.Equal(when, copy.RetiredAt);
    }

    [Fact]
    public void A_copy_knows_which_set_it_is_a_copy_of()
    {
        Guid titanic = Guid.CreateVersion7();

        Copy copy = Copy.Register(titanic, LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New, 9200);

        Assert.Equal(titanic, copy.CatalogSetId);
        Assert.Equal(9200, copy.BaselineWeightGrams);
    }

    [Fact]
    public void A_copy_belonging_to_no_set_is_not_a_copy_of_anything()
    {
        Assert.Throws<ArgumentException>(() =>
            Copy.Register(Guid.Empty, LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New, 9200));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_copy_that_was_never_weighed_is_not_registered(int grams)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Copy.Register(Guid.CreateVersion7(), LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New, grams));
    }
}
```

### The helper, and why thirteen call sites are not in this diff

`Copy.Register` has twenty-one call sites. Thirteen of them are in `CopyGradeTests` and
`CopyStatusTests`, which are about **grades and status transitions** and have no opinion whatsoever
about which set a box belongs to or what it weighs. Making all thirteen name two values they do not
care about is how a test suite becomes unreadable one refactor at a time.

```csharp
// tests/BrickShare.Catalog.UnitTests/ACopy.cs — new file
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

/// <summary>
/// A copy for tests that are about something else. Episode 29 gave <see cref="Copy.Register"/> two
/// arguments that the grade and status tests have no opinion about, and a named default is how you
/// keep "no opinion" from being spelled out thirteen times.
/// </summary>
internal static class ACopy
{
    /// <summary>A boxed Titanic, on the scale, in grams.</summary>
    public const int Grams = 9200;

    public static Copy Graded(ConditionGrade grade) =>
        Copy.Register(Guid.CreateVersion7(), LabelCode.Mint(), grade, Grams);
}
```

`CopyGradeTests.cs` and `CopyStatusTests.cs` are then **one find-and-replace each**, and this is the
one place in the course where a regular expression is the honest way to do it. Regex mode on:

```
Find:    Copy\.Register\(LabelCode\.Parse\("[^"]+"\), 
Replace: ACopy.Graded(
```

Thirteen call sites, no judgement required on any of them. Every remaining `Copy.Register` in the
test projects is now a call that genuinely means it.

### Green

```csharp
// src/Catalog/BrickShare.Catalog.Domain/Copy.cs — as it is, the top of the class
    private Copy(LabelCode label, ConditionGrade grade)
    {
        Id = Guid.CreateVersion7();
        Label = label;
        Grade = grade;
        Status = CopyStatus.Available;
    }

    public Guid Id { get; }

    public LabelCode Label { get; }
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/Copy.cs — replace it
    private Copy(Guid catalogSetId, LabelCode label, ConditionGrade grade, int baselineWeightGrams)
    {
        Id = Guid.CreateVersion7();
        CatalogSetId = catalogSetId;
        Label = label;
        Grade = grade;
        BaselineWeightGrams = baselineWeightGrams;
        Status = CopyStatus.Available;
    }

    public Guid Id { get; }

    /// <summary>
    /// Which catalog set this is a box of. An id, not a <see cref="CatalogSet"/>: a copy is its own
    /// aggregate, and episode 31's retirement must not need the set loaded to change a status.
    /// </summary>
    public Guid CatalogSetId { get; }

    /// <summary>
    /// What this box weighed when it was known complete. Per copy rather than per set, because a
    /// replacement manual or a repacked box shifts the number, and a shared reference would make
    /// those copies read as short on every future return (IDEA.md, "Baseline weight").
    /// </summary>
    public int BaselineWeightGrams { get; }

    public LabelCode Label { get; }
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/Copy.cs — as it is
    public static Copy Register(LabelCode label, ConditionGrade startingGrade)
    {
        ArgumentNullException.ThrowIfNull(label);

        return new Copy(label, startingGrade);
    }
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/Copy.cs — replace it
    public static Copy Register(
        Guid catalogSetId, LabelCode label, ConditionGrade startingGrade, int baselineWeightGrams)
    {
        ArgumentNullException.ThrowIfNull(label);

        if (catalogSetId == Guid.Empty)
        {
            throw new ArgumentException("A copy is a copy of something.", nameof(catalogSetId));
        }

        // Grams, and a box weighs more than nothing. This guard duplicates a FluentValidation rule
        // written in step 5, on purpose: the validator protects the HTTP client from itself, and
        // this protects the invariant from every caller that is not an HTTP request.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baselineWeightGrams);

        return new Copy(catalogSetId, label, startingGrade, baselineWeightGrams);
    }
```

### The guard that is an exception rather than a domain rule

Three `ArgumentException`s and no `DomainRuleViolationException`, in a codebase where episode 23
went out of its way to introduce the latter. The split is the one `CatalogSet.Catalogue` already
uses and it is worth thirty seconds:

- A **domain rule violation** is a sentence about the business a shop assistant could read: *a copy
  on rent cannot be retired*, *grades only fall*. Episode 23's handler turns those into a `409` with
  the message in the body, because the message is for a person.
- An **argument exception** is a statement about the caller being broken: a copy of no set, a weight
  of minus four. Nobody is owed an explanation in the response body, because a well-formed request
  cannot produce one — step 5's validator sees those first and answers `400`.

Green. Run the unit tests: `CopyIdentityTests` has six, `CopyGradeTests` and `CopyStatusTests` are
untouched in meaning and green through the helper.

---

## Step 3 — Red: the label nobody types

`LabelCode` has been in the codebase since episode 13 and has only ever *parsed*. Its own summary
comment has been promising the other half since then, and pointing at the wrong episode while it
did:

```csharp
// src/Catalog/BrickShare.Catalog.Domain/LabelCode.cs — as it is
/// LEGO boxes carry no per-unit serial number, so a copy's identity has to be invented by
/// the shop and stuck on the outside (UC-1.2). Minting happens in episode 23; this type is
/// only the format.
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/LabelCode.cs — replace it
/// LEGO boxes carry no per-unit serial number, so a copy's identity has to be invented by
/// the shop and stuck on the outside (UC-1.2). This type is the format and, from episode 29,
/// the mint.
```

Fix the number while it is on screen. Episode 27 made this point about a comment in `Program.cs`
and it is the same point: **a comment naming the wrong episode is how a reader decides the whole
file is untrustworthy.** There is one more of these in `Program.cs` right now — it says
"Episode 34 wires the other end of this" about the `traceId` extension, and observability is
episode 39.

Three tests, appended to the existing file:

```csharp
// tests/BrickShare.Catalog.UnitTests/LabelCodeTests.cs — append inside the class
    [Fact]
    public void A_minted_label_is_a_label()
    {
        LabelCode minted = LabelCode.Mint();

        Assert.True(LabelCode.TryParse(minted.Value, out _));
    }

    [Fact]
    public void Two_minted_labels_are_different()
    {
        Assert.NotEqual(LabelCode.Mint(), LabelCode.Mint());
    }

    [Theory]
    [MemberData(nameof(EveryCharacterTheMinterCanEmit))]
    public void Every_character_the_minter_can_emit_is_one_the_parser_accepts(char character)
    {
        Assert.True(LabelCode.TryParse($"BRK-{new string(character, 6)}", out _));
    }

    public static TheoryData<char> EveryCharacterTheMinterCanEmit()
    {
        TheoryData<char> characters = new();

        foreach (char character in LabelCode.Alphabet)
        {
            characters.Add(character);
        }

        return characters;
    }
```

That third test is the one to talk about. It passes the moment it is written, and it is not TDD —
it is the other kind of test `CLAUDE.md` names, the one that **describes a rule** rather than
driving a design. The rule is *the minter and the parser share one alphabet*, and without it the
two can drift apart by one character and produce label codes that this service refuses to read
back. Both kinds belong in the suite.

### Green

```csharp
// src/Catalog/BrickShare.Catalog.Domain/LabelCode.cs — as it is, the bottom of the type
    // 0/O, 1/I/L and U are absent from the alphabet on purpose. This code is printed on a
    // box, scanned at a counter, and read down a phone when the scanner will not read it —
    // so the two characters people confuse most are not in it at all.
    [GeneratedRegex("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$")]
    private static partial Regex Pattern();
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/LabelCode.cs — replace it
    /// <summary>
    /// Thirty characters. 0/O, 1/I/L and U are absent on purpose: this code is printed on a box,
    /// scanned at a counter, and read down a phone when the scanner will not read it, so the
    /// characters people confuse most are not in it at all.
    /// </summary>
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int Length = 6;

    /// <summary>
    /// A new identity for a box. 30^6 is 729 million codes, and they are drawn at random rather
    /// than issued in sequence: neighbouring boxes one character apart is the worst property a
    /// scanned identifier can have, because a single mis-read becomes a valid other box.
    /// </summary>
    public static LabelCode Mint() =>
        new($"BRK-{RandomNumberGenerator.GetString(Alphabet, Length)}");

    // One alphabet, used by both halves of the type. A second copy of this string inside the
    // pattern is a bug with a delay on it.
    [GeneratedRegex($"^BRK-[{Alphabet}]{{{Length}}}$")]
    private static partial Regex Pattern();
```

One new `using`, and it is the interesting one:

```csharp
// src/Catalog/BrickShare.Catalog.Domain/LabelCode.cs — with the other usings at the top
using System.Security.Cryptography;
```

> **If the source generator objects to the interpolated pattern** — it requires a compile-time
> constant, and a `$"…"` built only from `const`s is one, but toolchains have opinions — put the
> literal back in the attribute and leave `Alphabet` where it is. The third test is exactly the
> thing that then keeps the two honest, which is why it was worth writing.

### Two honest notes about `Mint`

**`RandomNumberGenerator`, and not because the code is a secret.** It is printed on the outside of
the box; anybody in the shop can read it. The reason is narrower: `Random.Shared` is fine here too,
and `RandomNumberGenerator.GetString` is the API that cannot be got subtly wrong — no seeding, no
modulo bias across a 30-character alphabet. A label code that is hard to guess is a small bonus
that nothing in this system is allowed to depend on. **Episode 38 protects these endpoints with
Entra ID; an unguessable identifier is not access control.**

**No `ILabelCodeMinter`, no DI, no fake.** An interface here would buy substitutability that
nothing wants: the tests assert a *format*, not a value, and there is no second minting strategy in
the plan. The moment a second shop needs its own prefix, this becomes an interface in one commit —
and until then it is a static method a reader can follow without leaving the file. Episode 22 made
the same call about assembly scanning, and episode 30 is where that one flips.

---

## Step 4 — The columns, and the first migration that could hurt

**Not test-driven, and it does not need to be.** This is mapping and a generated migration —
`CLAUDE.md`'s "infrastructure and configuration" exemption. The behaviour it supports was driven by
the tests in step 2, and it is the integration tests in step 5 that will prove the mapping works.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CopyConfiguration.cs — after the HasKey block
        builder.Property(copy => copy.CatalogSetId)
            .HasColumnName("catalog_set_id")
            .IsRequired();

        /*
         A foreign key with no navigation property on either end. EF needs no CatalogSet.Copies
         collection to write this constraint, and there is deliberately not one: a set with three
         hundred copies must not be materialised to catalogue a fourth, and episode 31 must be able
         to retire a box without loading the set it belongs to.

         Restrict, not the Cascade that a required foreign key gets by convention. Deleting a
         catalog set is not a thing this service does — and if it ever becomes one, the database
         should refuse rather than quietly take the rental history of every copy with it.
        */
        builder.HasOne<CatalogSet>()
            .WithMany()
            .HasForeignKey(copy => copy.CatalogSetId)
            .HasConstraintName("fk_copies_catalog_set_id")
            .OnDelete(DeleteBehavior.Restrict);

        // EF would index the foreign key by convention. Naming it keeps every index in this
        // database spelled the way the rest of the schema is spelled.
        builder.HasIndex(copy => copy.CatalogSetId)
            .HasDatabaseName("ix_copies_catalog_set_id");

        builder.Property(copy => copy.BaselineWeightGrams)
            .HasColumnName("baseline_weight_grams")
            .IsRequired();
```

`int` grams, and not `decimal` kilograms. Weight is a count of grams from a counter-top scale, there
is no fractional gram to lose, and episode 16's `numeric(10,2)` argument was about **money**, where
a hundredth is a legal unit. Reaching for `decimal` because a value is physical is cargo cult.

```bash
dotnet ef migrations add AddCopySetAndBaselineWeight \
  --project src/Catalog/BrickShare.Catalog.Api
```

Read the generated file on camera. Two `AddColumn` calls, an index, a foreign key — and the thing
worth pointing at:

```csharp
// src/Catalog/…/Migrations/…_AddCopySetAndBaselineWeight.cs — generated, read it, do not edit it
            migrationBuilder.AddColumn<Guid>(
                name: "catalog_set_id",
                table: "copies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
```

**This is the first migration in the course that would corrupt data if the table had any.** A
not-null column added to a populated table has to be given a value for every existing row, and EF's
answer is `Guid.Empty` — a foreign key pointing at a set that does not exist, which Postgres will
then refuse, so the migration fails at deploy time rather than lying.

Here it is harmless and stays as generated: `copies` is empty in every environment, because nothing
has ever been able to write to it. **Say the general shape anyway**, because students will hit it
with real rows: add the column nullable, backfill it in a second statement, then alter it to not
null — three migrations' worth of thinking in one file. Episode 33 does exactly that for themes,
against data that actually exists.

### The integration tests that the foreign key just broke

`CopyPersistenceTests` writes copies straight through `DbContext`, and every one of those rows now
points at a `catalog_sets` row that has to be there. That is the constraint doing its job in the
place it is cheapest to find out.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Persistence/CopyPersistenceTests.cs — bottom of the class
    /// <summary>
    /// A set for copies to belong to. The foreign key added in episode 29 means there is no such
    /// thing as a copy of nothing, including in a test that is about something else.
    /// </summary>
    private async Task<Guid> ACataloguedSetAsync()
    {
        CatalogSet set = CatalogSet.Catalogue(
            SetNumber.Parse("10294-1"), "Titanic", "Icons", 2021, 9092,
            new Money(629.99m), new Money(60.00m), 7, 18);

        await using CatalogDbContext context = Database.NewDbContext();
        context.Sets.Add(set);
        await context.SaveChangesAsync();

        return set.Id;
    }
```

Then each of the three tests gains one line and its `Copy.Register` calls gain two arguments. The
first, in full, as the pattern for the other two:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Persistence/CopyPersistenceTests.cs — as it is
    [Fact]
    public async Task A_registered_copy_comes_back_as_the_copy_that_was_registered()
    {
        Copy registered = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good);
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Persistence/CopyPersistenceTests.cs — replace it
    [Fact]
    public async Task A_registered_copy_comes_back_as_the_copy_that_was_registered()
    {
        Guid setId = await ACataloguedSetAsync();

        Copy registered = Copy.Register(setId, LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good, 9200);
```

and one extra assertion at the end of it, because a column nobody reads back is a column nobody
knows is mapped:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Persistence/CopyPersistenceTests.cs — after the last Assert
        Assert.Equal(setId, read.CatalogSetId);
        Assert.Equal(9200, read.BaselineWeightGrams);
```

`Two_copies_cannot_carry_the_same_label_code` and
`The_second_of_two_people_writing_to_the_same_copy_is_refused` take the identical treatment: one
`Guid setId = await ACataloguedSetAsync();` at the top, and the new arguments on each
`Copy.Register`. Neither test changes what it asserts — the unique index and the `xmin` token are
exactly as episode 16 left them.

Add the `using BrickShare.Catalog.Domain;` if the analyzer asks; `SetNumber` and `Money` are new to
this file.

Run the integration tests. Green, and the schema now has a shape for a box that belongs somewhere.

---

## Step 5 — Red: one box, over HTTP

### First, the helper episode 28 left private

Every test that needs a catalogued set has to make two calls to get one, and that code currently
lives as a private method on `CatalogueSetTests`. A second test class needs it now, so it moves.
This is the refactor leg, and it is being done because a second caller arrived — not in
anticipation of one.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogFlow.cs — new file
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;

namespace BrickShare.Catalog.IntegrationTests;

/// <summary>
/// The two-call flow episodes 27 and 28 built, as one method. Extensions on
/// <see cref="CatalogDatabase"/> rather than helpers on a base class, because a test that needs
/// neither should not inherit them.
/// </summary>
internal static class CatalogFlow
{
    /// <summary>
    /// Teaches the stub about the Titanic and looks it up. Returns the lookup id that
    /// <c>POST /catalog/sets</c> requires.
    /// </summary>
    public static async Task<Guid> LookUpTitanicAsync(this CatalogDatabase database, HttpClient client)
    {
        database.Rebrickable.Sets["10294-1"] = new
        {
            set_num = "10294-1",
            name = "Titanic",
            year = 2021,
            theme_id = 252,
            num_parts = 9092,
            set_img_url = "https://cdn.rebrickable.com/media/sets/10294-1.jpg"
        };

        database.Rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)null };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        response.EnsureSuccessStatusCode();

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        return draft.LookupId;
    }

    /// <summary>
    /// Both calls. Returns the id of a catalogued set, which is what a copy needs to belong to.
    /// </summary>
    public static async Task<Guid> CatalogueTitanicAsync(this CatalogDatabase database, HttpClient client)
    {
        Guid lookupId = await database.LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets", new
        {
            lookupId,
            retailPrice = 629.99m,
            baseRentalPrice = 60.00m,
            minimumRentalDays = 7,
            minimumAge = 18
        });

        response.EnsureSuccessStatusCode();

        CatalogSetResponse? created = await response.Content.ReadFromJsonAsync<CatalogSetResponse>();

        Assert.NotNull(created);

        return created.Id;
    }
}
```

In `CatalogueSetTests.cs`, **delete the private `LookUpTitanicAsync` method entirely** and let the
five calls to it bind to the extension instead:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — five occurrences
        Guid lookupId = await LookUpTitanicAsync(client);
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — replace all five
        Guid lookupId = await Database.LookUpTitanicAsync(client);
```

Six tests in that file, still green, and not one of them changed what it asserts.

### Now the endpoint that does not exist

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RegisterCopyTests.cs — new file
using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Copies;

public class RegisterCopyTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_registered_copy_comes_back_with_a_label_nobody_typed()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { grade = "New", baselineWeightGrams = 9200 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        CopyResponse? copy = await response.Content.ReadFromJsonAsync<CopyResponse>(Database.Api.Json);

        Assert.NotNull(copy);
        Assert.Equal(setId, copy.CatalogSetId);
        Assert.Equal(ConditionGrade.New, copy.Grade);
        Assert.Equal(CopyStatus.Available, copy.Status);
        Assert.Equal(9200, copy.BaselineWeightGrams);

        // The shape, not the value. The value is the server's business and the client has no way
        // to have an opinion about it.
        Assert.Matches("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$", copy.LabelCode);
    }

    [Fact]
    public async Task The_shop_that_bought_two_boxes_calls_twice_and_gets_two_labels()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        CopyResponse first = await RegisterAsync(client, setId);
        CopyResponse second = await RegisterAsync(client, setId);

        Assert.NotEqual(first.LabelCode, second.LabelCode);
        Assert.NotEqual(first.Id, second.Id);

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(2, await dbContext.Copies.CountAsync(copy => copy.CatalogSetId == setId));
    }

    [Fact]
    public async Task A_copy_of_a_set_nobody_catalogued_is_not_a_copy_of_anything()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{Guid.CreateVersion7()}/copies",
            new { grade = "New", baselineWeightGrams = 9200 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("catalogue", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_box_nobody_put_on_the_scale_is_refused()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { grade = "New", baselineWeightGrams = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("baselineWeightGrams", problem.Errors.Keys);
    }

    // Not static: it needs Database for the serializer options, which the end of this step explains.
    private async Task<CopyResponse> RegisterAsync(HttpClient client, Guid setId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { grade = "New", baselineWeightGrams = 9200 });

        response.EnsureSuccessStatusCode();

        CopyResponse? copy = await response.Content.ReadFromJsonAsync<CopyResponse>(Database.Api.Json);

        Assert.NotNull(copy);

        return copy;
    }
}
```

Red, and it does not compile, for two reasons: there is no `CopyResponse`, and there is no
`Database.Api.Json` either. The first is the endpoint, written next. The second is the more
interesting of the two and is the last thing this step does.

### Green

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — new file
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Endpoints;

public static class CopyEndpoints
{
    public static RouteGroupBuilder MapCopies(this IEndpointRouteBuilder routes)
    {
        // Nested under the set, because a copy cannot exist without one and the url should not
        // pretend otherwise. Episode 31 adds /catalog/copies/{id} for the operations that only
        // need the box.
        RouteGroupBuilder group = routes.MapGroup("/catalog/sets/{setId:guid}/copies")
            .WithTags("Copies");

        group.MapPost("/", RegisterAsync);

        return group;
    }

    private static async Task<Results<Created<CopyResponse>, ValidationProblem, ProblemHttpResult>>
        RegisterAsync(
            Guid setId,
            RegisterCopyRequest request,
            IValidator<RegisterCopyRequest> validator,
            CatalogDbContext database,
            CancellationToken cancellationToken)
    {
        ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        bool setExists = await database.Sets.AnyAsync(set => set.Id == setId, cancellationToken);
        if (!setExists)
        {
            // A 404, and episode 28 said 422 for a lookupId that did not exist. Same ladder,
            // different rung: that id was in the body, describing a request that referenced
            // nothing. This one is in the path, so it is the addressed resource that is absent.
            return TypedResults.Problem(
                title: "No such set",
                detail: $"Set {setId} is not catalogued. Catalogue it first at /api/v1/catalog/sets.",
                statusCode: StatusCodes.Status404NotFound);
        }

        Copy copy = Copy.Register(setId, LabelCode.Mint(), request.Grade, request.BaselineWeightGrams);

        database.Copies.Add(copy);
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/v1/catalog/copies/{copy.Id}", CopyResponse.From(copy));
    }
}

/// <summary>
/// What staff send to register one box. No label: minting is the server's job, and episode 28's
/// rule holds — a field the client must not choose is not a field the client can send.
/// </summary>
public sealed record RegisterCopyRequest(ConditionGrade Grade, int BaselineWeightGrams);

public sealed record CopyResponse(
    Guid Id,
    Guid CatalogSetId,
    string LabelCode,
    ConditionGrade Grade,
    CopyStatus Status,
    int BaselineWeightGrams)
{
    public static CopyResponse From(Copy copy) => new(
        copy.Id,
        copy.CatalogSetId,
        copy.Label.Value,
        copy.Grade,
        copy.Status,
        copy.BaselineWeightGrams);
}
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/RegisterCopyRequestValidator.cs — new file
using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class RegisterCopyRequestValidator : AbstractValidator<RegisterCopyRequest>
{
    /// <summary>
    /// Fifty kilograms. The heaviest boxed LEGO set weighs about fifteen, so this is not a claim
    /// about LEGO — it is a typo filter. A baseline of 92000 grams instead of 9200 makes every
    /// future return of that copy look catastrophically short.
    /// </summary>
    public const int MaximumBaselineWeightGrams = 50_000;

    public RegisterCopyRequestValidator()
    {
        // The backstop for the numeric form. A grade sent as "Sparkly" never reaches this rule —
        // the JSON reader refuses it first — but a grade sent as 99 deserializes happily.
        RuleFor(request => request.Grade).IsInEnum()
            .WithMessage("A grade is one of New, Excellent, Good or Fair.");

        RuleFor(request => request.BaselineWeightGrams)
            .InclusiveBetween(1, MaximumBaselineWeightGrams)
            .WithMessage(
                $"A baseline weight is in grams, between 1 and {MaximumBaselineWeightGrams}. "
                + "Weigh the box while it is known complete.");
    }
}
```

Wire it up, beside the two registrations that are already there:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — as it is
builder.Services.AddScoped<IValidator<CatalogueSetRequest>, CatalogueSetRequestValidator>();
builder.Services.AddScoped<IValidator<LookupRequest>, LookupRequestValidator>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — replace it
builder.Services.AddScoped<IValidator<CatalogueSetRequest>, CatalogueSetRequestValidator>();
builder.Services.AddScoped<IValidator<LookupRequest>, LookupRequestValidator>();
builder.Services.AddScoped<IValidator<RegisterCopyRequest>, RegisterCopyRequestValidator>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — as it is
v1.MapCatalogSets();
v1.MapCatalogLookups();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — replace it
v1.MapCatalogSets();
v1.MapCatalogLookups();
v1.MapCopies();
```

**Three validators now, registered by hand.** Episode 22 said scanning becomes right at three and
that episode 30 swaps it in. It is three. Leave the line alone for one more episode — episode 30
opens on this exact spot.

### Still red — the enum on the wire

```
Assert.Equal() Failure: Values differ
Expected: Created
Actual:   BadRequest
```

The body says `"grade": "New"` and `System.Text.Json` reads enums as **numbers** by default, so
`"New"` is not a value it will accept at all. The fix is one line, and the reason it is the right
line rather than "make the test send `0`" is worth the thirty seconds:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — after the ValidatorOptions block
// Grades and statuses cross the wire as "New" and "Available", never as 0 and 1. An ordinal is a
// position in a C# declaration: insert a grade between Excellent and Good and every client in the
// world silently changes its mind about what it is asking for. The database already made this
// call — CopyConfiguration stores both columns as strings — and the wire now agrees with it.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — with the other usings at the top
using System.Text.Json.Serialization;
```

### Still red — and now it is the test's fault

```
System.Text.Json.JsonException : The JSON value could not be converted to
BrickShare.Catalog.Domain.ConditionGrade. Path: $.grade
```

**That converter changed both directions.** The request parses now, the endpoint returns `201`, and
the test falls over reading the response — before a single `Assert` runs, which is why the message
names no assertion at all.

`ReadFromJsonAsync<CopyResponse>()` with no arguments uses `JsonSerializerDefaults.Web`: camelCase,
case-insensitive, and **still numbers-only for enums**. The server was configured; the client was
not. Nothing has caught this in nine episodes of integration tests because
`CatalogSetResponse` and `LookupResponse` are strings, numbers and `Guid`s — **this is the first
test in the course to read an enum off the wire.**

The temptation is to build a matching `JsonSerializerOptions` in the test project. Do not: that is
step 3's alphabet argument again, **a second copy of a decision is a bug with a delay on it**, and a
test that restates the wire format keeps passing on the day somebody deletes the converter from
`Program.cs`. Read the real options out of the running host instead.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogApiFactory.cs — a new member, below ConfigureWebHost
    /// <summary>
    /// The options this API serialises with, read back out of the running host rather than restated
    /// here. A test is a client, and a client that guesses the wire format is a test that can pass
    /// for the wrong reason.
    /// </summary>
    public JsonSerializerOptions Json =>
        Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogApiFactory.cs — with the other usings at the top
using System.Text.Json;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
```

**`JsonOptions` here is `Microsoft.AspNetCore.Http.Json.JsonOptions`** — the minimal-API one that
`ConfigureHttpJsonOptions` writes to. There is an MVC type with the identical name, and picking the
wrong one compiles, resolves, and hands back an options object nothing in this application ever
configured. Worth pointing at, because "it compiled and it is still empty" is a bad half-hour.

Green, all four. And the fourth test — the weight of zero — is worth pausing on, because the
`400` it asserts is produced by the validator, while the identical mistake made from anywhere else
in the codebase is caught by step 2's `ArgumentOutOfRangeException`. Two gates, different audiences,
and neither one is redundant.

### What just happened, in one line

One line of server configuration made a response the test could not read, and the fix was to stop
the test having an opinion of its own about the format. **Both ends of a wire have to agree, and the
cheapest way to agree is for one of them to ask the other.**

---

## Step 6 — When two mints collide

729 million codes, and a shop with ten thousand boxes has about one chance in seventy thousand of
minting a duplicate on any given registration. Rare is not never, and what happens today is a
`DbUpdateException` out of `SaveChangesAsync` and a `500` in front of a staff member who did
nothing wrong.

Episode 16 put a unique index on `label_code` and called it the arbiter. This is what having an
arbiter is for:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — as it is, in RegisterAsync
        Copy copy = Copy.Register(setId, LabelCode.Mint(), request.Grade, request.BaselineWeightGrams);

        database.Copies.Add(copy);
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/v1/catalog/copies/{copy.Id}", CopyResponse.From(copy));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — replace it
        Copy copy = await RegisterWithAMintedLabelAsync(setId, request, database, cancellationToken);

        return TypedResults.Created($"/api/v1/catalog/copies/{copy.Id}", CopyResponse.From(copy));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — below RegisterAsync
    private const int MintAttempts = 3;

    /// <summary>
    /// Mint, insert, and on the one-in-seventy-thousand collision, mint again. Three attempts makes
    /// the chance of failing about one in 4x10^14, which is far enough below every other way this
    /// request can fail to stop thinking about it.
    /// </summary>
    private static async Task<Copy> RegisterWithAMintedLabelAsync(
        Guid setId,
        RegisterCopyRequest request,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MintAttempts; attempt++)
        {
            Copy copy = Copy.Register(
                setId, LabelCode.Mint(), request.Grade, request.BaselineWeightGrams);

            database.Copies.Add(copy);

            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return copy;
            }
            catch (DbUpdateException ex) when (IsLabelAlreadyTaken(ex) && attempt < MintAttempts)
            {
                // A failed SaveChanges leaves the entity Added. Without this the retry writes two
                // rows, one of which still carries the label that just collided.
                database.Entry(copy).State = EntityState.Detached;
            }
        }

        // The last attempt either returns or lets its exception out, because the filter above stops
        // catching once attempt reaches MintAttempts. The compiler cannot see that: definite-return
        // analysis does not reason about exception filters, so it needs to be told.
        throw new UnreachableException();
    }

    private static bool IsLabelAlreadyTaken(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_copies_label_code"
        };
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — with the other usings
using System.Diagnostics;

using Npgsql;
```

### The loop that had to name its own counter

`for (int attempt = 1; ; attempt++)` compiles, runs correctly, and trips **Sonar S1994** —
*this loop's stop incrementer updates `attempt` but the stop condition doesn't test any variables.*
The rule is right, and it is worth thirty seconds because the reason is not style: a `for` header is
a three-part contract — start here, keep going while this, step by that — and a loop that fills in
two of the three is telling the reader the counter matters while hiding what it is for. Every reader
now has to go into the body to find out when it stops.

Naming `MintAttempts` in the condition costs one `throw` at the bottom, and that `throw` is the
interesting part: **definite-return analysis does not reason about exception filters.** The compiler
cannot see that `attempt < MintAttempts` in the `when` clause is what makes the third failure
escape, so it believes the loop can finish normally. `UnreachableException` is the type .NET added
to say *I have checked, and this cannot happen* — better than `return null!`, and better than a
`while (true)` that hides the bound again.

### The same exception, meaning the opposite thing

Put this beside `CatalogSetEndpoints.IsAlreadyCatalogued` on screen. The two methods are nearly
character-for-character identical — a `PostgresException`, a unique violation, a constraint name —
and they mean completely different things:

| | Episode 24: `ix_catalog_sets_set_number` | Here: `ix_copies_label_code` |
| --- | --- | --- |
| Who chose the value | The client, via a lookup | The server, at random |
| What the violation means | *You asked for something that is already true* | *I was unlucky* |
| The right response | `409`, with an explanation | Nothing. Try again and say nothing |

**A unique violation is not an error code, it is an answer to a question**, and the question is
different in the two places. A codebase that maps every `UniqueViolation` to a `409` in one shared
helper would turn a one-in-seventy-thousand internal retry into a conflict the staff member is
asked to resolve.

**Not tested, and that is deliberate.** A test for this branch would have to force a collision —
either by seeding 729 million rows, or by injecting a fake minter that returns the same code twice,
which would mean introducing the `ILabelCodeMinter` step 3 argued against, to test a branch, in an
episode that argued for deleting code paths nobody needs. The branch is three lines and reads
correctly. **That is a trade, not a free choice, and episode 30 is where it gets paid off**: a batch
mints several labels inside one transaction, and the collision becomes reachable from a test that
registers copies in bulk.

---

## Step 7 — Run it

Not test-driven: a request collection is not code. Append to what episode 28 left:

```http
### BrickShare.Catalog.Api.http — append

@setId = paste-the-id-from-request-2

### 7. Register one box. 201, and the label code in the response was minted by the server.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{ "grade": "New", "baselineWeightGrams": 9200 }

### 8. The second box the shop bought the same day. A second call, a different label.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{ "grade": "New", "baselineWeightGrams": 9187 }

### 9. A set nobody catalogued. 404: the id is in the path, so the resource really is absent.
POST {{host}}/api/v1/catalog/sets/01931f3c-0000-7000-8000-000000000000/copies
Content-Type: application/json

{ "grade": "New", "baselineWeightGrams": 9200 }

### 10. A box nobody weighed. 400, one error key.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{ "grade": "New", "baselineWeightGrams": 0 }

### 11. A label the client tried to choose. 201 — and the label in the response is not this one.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{ "grade": "Good", "baselineWeightGrams": 9150, "labelCode": "BRK-AAAAAA" }
```

Run 7, 8 and 11 on camera. Request 11 is the closing shot: the client sent a label, got a `201`, and
the response carries a different code — because `RegisterCopyRequest` has no `LabelCode` member and
the extra JSON is read by nobody. Episode 28's rule, arrived at from the other direction. The two
weights in 7 and 8 differ by thirteen grams, which is the point of the field: two boxes of the same
set, weighed separately, because they are not the same object.

```sql
select label_code, grade, status, baseline_weight_grams from copies;
```

Three rows, three labels nobody typed.

---

## What this episode is not

**No batch.** Four boxes is four calls, and every one of them is a separate transaction — so a
till-side interruption after the second leaves two copies registered and two not. That is a real
defect and it is being left in deliberately for one episode, because **episode 30 refactors this
endpoint into the batch version and the "*n* copies or none" property is much easier to explain
when the thing it fixes is on screen and misbehaving.** *n* is whatever was in the delivery: one,
two, seventeen.

**No retire.** `Copy.Retire` has existed and been unit-tested since episode 15, including the rule
that a copy out on rent cannot be retired, and it still has no HTTP in front of it. Episode 31.

**No `GET`.** There is no way to list the copies of a set, or to look one up by scanning its label,
which is UC-1.4 and squarely the read API's job — episode 34. `TypedResults.Created` points at
`/api/v1/catalog/copies/{id}`, a route that does not exist yet, for the same reason episode 28 left
its `Location` header pointing at nothing: inventing an endpoint to make a header true is building
a feature to satisfy a string.

**No unique constraint on (set, anything).** A shop can register the same physical box twice by
accident and the database will not stop it, because there is nothing to compare — the box has no
serial number, which is the entire reason label codes exist. The protection is procedural: the
label is printed and stuck on at registration, so a box with a sticker has been registered.

**No `ILabelCodeMinter`, still.** See step 3. Episode 30 is allowed to change its mind if the batch
makes it necessary; nothing so far has.

**No authorization.** Anybody who can reach the service can register copies, until **episode 38**.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test --filter FullyQualifiedName~UnitTests` | Green — `CopyIdentityTests` has six, `LabelCodeTests` has ten |
| `dotnet test` | Green — four in `RegisterCopyTests`, six in `CatalogueSetTests`, three in `CopyPersistenceTests` |
| `curl` the endpoint and read the raw body | `"grade": "New"`, not `"grade": 0` — the wire is words |
| `dotnet ef migrations list` | `AddCopySetAndBaselineWeight` is last |
| `\d copies` in psql | `catalog_set_id uuid not null`, `baseline_weight_grams integer not null`, `fk_copies_catalog_set_id` |
| `.http` request 7 | `201`, a `BRK-` label, `"status": "Available"` |
| `.http` requests 7 then 8 | Two different label codes |
| `.http` request 9 | `404`, `application/problem+json`, a `traceId` extension |
| `.http` request 10 | `400`, one error key: `baselineWeightGrams` |
| `.http` request 11 | `201`, and `label_code` in the database is **not** `BRK-AAAAAA` |
| `insert into copies (…) values (…, '<a random uuid>', …)` in psql | Refused by `fk_copies_catalog_set_id` |

The last row is the one to run on camera, because it is the only check here that proves something
the C# cannot: a copy of a set that does not exist is refused by the **database**, not by an `if`
in a handler somebody can delete.

## Next

[Episode 30 — All of them or none of them](catalog-api.md#episode-30--all-of-them-or-none-of-them):
the shop buys four Titanics and calls this endpoint four times, and the fourth call fails. The
endpoint is refactored to take a list of any length in one transaction — `RuleForEach` over a
collection is the case episode 22 said validation was waiting for, three validators finally make
`AddValidatorsFromAssemblyContaining` worth the loss of explicitness, and the validation call moves
out of the handlers into an endpoint filter.

The sentence to carry out of this one: **a copy is a thing, not a row in a description.** It belongs
to a set, it has an identity the shop issued because nobody else would, and it has a weight that was
true on the day it was weighed and belongs to that box alone.
