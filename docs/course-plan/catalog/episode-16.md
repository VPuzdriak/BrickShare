# Episode 16 — Postgres in Compose, EF Core mapping

← [Course plan](catalog-api.md) · Previous: [Episode 15 — The copy state machine](episode-15.md)

Twenty-one green tests, and not one of them survives a process restart. Every copy this course has
registered, reserved, inspected and retired lived for a few milliseconds inside a test runner and
then stopped existing. This episode gives the domain somewhere to be.

The part worth the episode is not the plumbing. It is that **persistence asks questions the domain
was allowed to leave implicit** — what is this thing's identity, when exactly did that happen, and
what stops two people writing to the same box at once. Three episodes of careful modelling did not
need to answer any of them. A table does.

**Done when** a `copies` table exists in a real Postgres running under Compose, created by a
migration committed as a reviewable file, with status and grade stored as **names** rather than
ordinals, a **database** uniqueness constraint on the label code, and `/health/ready` that goes
unhealthy when the database does.

## Before recording

- Episode 15 merged: `Copy` with eleven operations and `TransitionTo`, and `dotnet test` green.
- Docker running. `psql` on the path is useful but not required — `docker compose exec` reaches it.
- A branch.
- `docs/architecture/catalog.md` open at *Data — Azure Database for PostgreSQL*, especially
  *Things the schema must get right* and *EF Core, and no second data-access story*.
- The end of [`episode-15.md`](episode-15.md) open at *What this episode deliberately does not do*.
  Two of those bullets are debts this episode pays, and one is a promise made in its last
  paragraph.

**This episode is mostly not TDD, and it says so rather than pretending.** Packages, a Compose
service, a connection string, mapping configuration and a generated migration are exactly what
CLAUDE.md exempts: infrastructure, configuration and wiring with no behaviour of its own. Writing a
failing test for `ToTable("copies")` would be theatre — the assertion would be a transcription of
the line it is testing.

**Two steps are test-driven**, steps 4 and 5, and they are worth watching for what they are: not
persistence, but **domain behaviour that persistence forced into the open**. A copy's identity and
the moment of its retirement were always missing. Nothing had asked.

The real verification of the mapping is a running database and, in episode 17, a test against one.

---

## Step 0 — What a domain in memory cannot answer

Before the first line, three questions on camera. Read them out and let them sit, because the whole
episode is these three and nothing else:

1. **Two copies are registered. Which one is which?** `Copy` has a `LabelCode`, and that is a
   business fact printed on a box. It has no identity of its own. In memory this never came up:
   reference equality answered it for free, and no test ever needed two copies to be told apart
   after a round trip.
2. **A copy is retired. When?** `Retire()` sets a status and records nothing else.
   `docs/architecture/catalog.md` gives `copies` a `retired_at` column, because *how much stock did
   we retire last quarter* is a question a shop asks and a status alone cannot answer.
3. **Two staff scan the same box at the same time.** Episode 15 admitted this limit in its own
   words — *every guard in this class protects one object in-process* — and left it. It cannot be
   fixed in memory, because the fix is a property of the row.

None of these is a persistence detail. All three are business questions that an in-memory model was
never forced to answer, and this is the general shape of the thing: **a database does not add
requirements, it stops you deferring them.** That is worth more than any mapping API in this
episode, and it is why persistence arriving late in a course is not the same as persistence being
an afterthought in a design.

**This step is not code.**

---

## Step 1 — Postgres joins Compose

`docker-compose.yml` has not been opened since episode 4, and it has been carrying a comment
promising this moment:

> One service, and it looks like overkill for one service. It's the socket Postgres, Azurite and
> the Rebrickable stub plug into from episode 15 onward — adding it now costs this file and nothing
> else.

`docker-compose.yml`:

```yaml
services:
  catalog-api:
    build:
      context: .
      dockerfile: src/Catalog/BrickShare.Catalog.Api/Dockerfile
    ports:
      - "5080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      # Inside the Compose network the database answers to its service name. See step 2.
      ConnectionStrings__Catalog: "Host=postgres;Port=5432;Database=brickshare_catalog;Username=brickshare;Password=brickshare"
    depends_on:
      postgres:
        # Not "started" — "healthy". See below; this line is the whole reason the healthcheck
        # underneath it exists.
        condition: service_healthy

  postgres:
    image: postgres:18
    environment:
      POSTGRES_DB: brickshare_catalog
      POSTGRES_USER: brickshare
      POSTGRES_PASSWORD: brickshare
    ports:
      # Published for the humans, not for the API. psql, Rider's database tool and `dotnet ef`
      # all run on the host and all need a route in.
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U brickshare -d brickshare_catalog"]
      interval: 5s
      timeout: 3s
      retries: 10
    volumes:
      - catalog-postgres-data:/var/lib/postgresql/data

volumes:
  catalog-postgres-data:
```

Four lines deserve a sentence each.

**The image tag is pinned to a major version, not `latest`.** `postgres:latest` means the database
engine changes underneath the project on whatever day the image is rebuilt, which is the same class
of problem `global.json` was pinned against in episode 2. Pinning the major and letting patches
roll is the trade this repository has already chosen twice.

**The password is in the file in plain text, and it is not a secret.** Say this out loud, because
it looks exactly like the thing episode 18 will spend a whole episode arguing against. A credential
is a secret when it protects something; this one protects a database that exists on one laptop,
contains data anybody could regenerate, and is thrown away by `docker compose down -v`. The
credential that will protect the *real* database does not appear in this repository at all — not
here, not in Key Vault, not anywhere — because episode 18 uses a managed identity and there is no
password to store. **The rule is not "never write a password in a file". It is "never have a
password worth stealing".**

**`depends_on` with `condition: service_healthy`, not the bare form.** Plain `depends_on` waits for
the container to *start*, and a Postgres container is up for a second or two before it accepts
connections. The API then starts, fails to connect, and — depending on the day — either retries
into a working state or crashes in a way that looks intermittent and is blamed on EF. The
healthcheck turns "the process launched" into "the database answers", which is the thing that was
actually meant.

**A named volume, not a bind mount.** `./data:/var/lib/postgresql/data` also works and puts the
database files in the repository directory, where the file permissions differ per operating system,
`.gitignore` has to be told about them, and a stray `git clean` deletes the database. A named volume
is managed by Docker, works identically on every machine in the class, and is removed on purpose
with `docker compose down -v`.

**Not test-driven, and there is no honest way to make it so.** This is a YAML file describing four
containers. The test is `docker compose up`.

---

## Step 2 — One database, two hostnames

The API reaches Postgres by two different names depending on where it is running, and this trips
people badly enough to be worth its own step.

`src/Catalog/BrickShare.Catalog.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Catalog": "Host=localhost;Port=5432;Database=brickshare_catalog;Username=brickshare;Password=brickshare"
  }
}
```

**`localhost` here, `postgres` in Compose, and both are correct.** `dotnet run` on the host reaches
the database through the published port — `localhost:5432`. A container inside the Compose network
reaches it by service name — Docker's DNS resolves `postgres` to the container, and `localhost`
inside that container is the API container itself, which is running no database at all. The error
when this is wrong is `Connection refused (localhost:5432)` from inside a container, and it is one
of the most common first-day-with-Docker messages there is.

**And the override mechanism is worth naming, because it is used for the rest of the course.**
`ConnectionStrings__Catalog` in the environment maps onto `ConnectionStrings:Catalog` in
configuration — a double underscore where the colon goes, because colons are not legal in
environment variable names on every platform. Environment variables sit above JSON files in the
provider order, so the Compose value wins without the file needing to know Compose exists.

That ordering is the whole design: **the same image runs in both places and neither knows which**.
In episode 18, App Service supplies the same key from its own configuration and nothing in the
image changes.

`appsettings.json` — the one that ships to production — gets **no connection string at all**. There
is nothing to put in it, and nothing sensible to default to. Every environment supplies its own, and
an absent key means a context that cannot open a connection and a readiness check that says so —
which is a better failure than a default quietly pointing at something that happens to answer.

---

## Step 3 — Packages, and a tool pinned like everything else

Three packages into `Directory.Packages.props`:

```xml
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```

and referenced from the API project only:

`src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BrickShare.Catalog.Domain\BrickShare.Catalog.Domain.csproj" />
  </ItemGroup>
</Project>
```

**Note what did not change: `BrickShare.Catalog.Domain.csproj` is still one line.**

```xml
<Project Sdk="Microsoft.NET.Sdk" />
```

Episode 13 built that boundary and made the compiler enforce it with `using
Microsoft.AspNetCore.Http;` failing to resolve. This is the first episode where the boundary has
something real to keep out, and it is worth stopping on: **the domain does not reference EF Core, so
no domain type can be shaped by it.** No `[Key]` attribute, no `[Column("label_code")]`, no
`virtual` properties added for a lazy loader, no `ICollection<T>` chosen because a mapper prefers
it. The types written in episodes 13 to 15 are the types that get mapped, exactly as they are.

That is not a style preference. Attributes on domain types put persistence vocabulary in the file a
business rule lives in, and every one of them is a decision made by whoever is looking at the
database rather than whoever is looking at the rule. The plan says it in one line — *explicit
`IEntityTypeConfiguration` classes over attributes and convention* — and this is the reason.

**The `dotnet ef` tool, pinned in the repository:**

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef
```

This writes `.config/dotnet-tools.json`, which is committed. **Same discipline as `global.json`,
same reason.** A tool installed globally is a version that differs per machine and per CI runner,
and a migration generated by one version against a project built by another is a class of problem
nobody enjoys. `dotnet tool restore` makes a fresh clone identical to every other clone.

**Not test-driven.** These are package references and a tool manifest.

---

## Step 4 — Identity, and the question a table asks first (cycle 1)

Now the first of the two test-driven steps, and the reason it is test-driven: assigning identity is
something `Copy` *does*, and it is going to do it whether or not there is ever a database.

### Red

`tests/BrickShare.Catalog.UnitTests/CopyIdentityTests.cs`:

```csharp
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyIdentityTests
{
    [Fact]
    public void Two_registered_copies_have_different_identities()
    {
        Copy first = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);
        Copy second = Copy.Register(LabelCode.Parse("BRK-9H4M2N"), ConditionGrade.New);

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }
}
```

```
error CS1061: 'Copy' does not contain a definition for 'Id'
```

### Green

```csharp
    private Copy(LabelCode label, ConditionGrade grade)
    {
        Id = Guid.CreateVersion7();
        Label = label;
        Grade = grade;
        Status = CopyStatus.Available;
    }

    public Guid Id { get; }
```

### Three keys, and the two that were not chosen

The copy already has something unique about it: `LabelCode`, minted by BrickShare, printed on the
box, and about to get a uniqueness constraint of its own. **The obvious move is to make it the
primary key**, and it is worth taking seriously for a moment, because natural keys are not a
beginner's mistake — a key that means something is genuinely easier to read in a query and removes
a column.

It is not taken because of what `docs/IDEA.md` says the label *is*: a code the shop invented
because LEGO boxes carry no per-unit serial. **A thing the business invented is a thing the business
can change.** A label peels off and is reprinted; the alphabet gets a character added; a batch is
discovered mislabelled and reissued. With a natural key, every one of those is an update to a
primary key, which propagates to every foreign key referencing it — photographs, rental history in
another service, a URL somebody bookmarked. With a surrogate key it is an update to one column in
one row.

**And the third option, `bigint GENERATED ALWAYS AS IDENTITY`,** is the default in a great many
schemas and has the best index behaviour of the three. Two costs decide it here. First, the database
assigns it, so a `Copy` has no identity until it has been saved — the object exists, is valid, is
mid-way through a batch registration, and cannot yet be referred to. Second, sequential integers in
a URL are an enumeration: `/copies/1041` tells a customer roughly how much stock the shop has ever
owned, and `/copies/1042` is somebody else's box.

**So: a GUID, assigned in the constructor, and it is version 7 on purpose.** `Guid.NewGuid()`
produces a version 4 — random across the whole range — and random keys are actively bad for a B-tree
primary index: every insert lands in a different page, the pages that matter never stay in cache,
and the index fragments as the table grows. `Guid.CreateVersion7()` puts a millisecond timestamp in
the high bits, so ids generated near each other in time sort near each other, and inserts append to
the end of the index the way an identity column does. **It is the identity-column insert pattern
without the identity column's two costs**, at the price of eight extra bytes per row and a key
nobody can read out over the phone — which is what the label code is for.

Worth saying plainly, because it is the part that transfers: *the object assigns its own identity,
in its constructor, before anything is saved.* A `Copy` is fully itself the moment it is created. No
`copy.Id == 0` check anywhere in the codebase means "not saved yet", and no code path has to care
whether a save has happened. **A null or zero id is a state that has to be handled everywhere it can
occur, and this design does not let it occur.**

---

## Step 5 — `retired_at`, and who owns the clock (cycles 2–3)

The first debt from episode 15's list:

> **No persistence, and no `retired_at`.** The architecture document gives `copies` a `retired_at`
> column, because *when* a copy was retired is a question stock reports ask.

### Cycle 2 — a copy on the shelf was never retired

```csharp
    [Fact]
    public void A_copy_on_the_shelf_has_no_retirement_date()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        Assert.Null(copy.RetiredAt);
    }
```

```
error CS1061: 'Copy' does not contain a definition for 'RetiredAt'
```

```csharp
    public DateTimeOffset? RetiredAt { get; private set; }
```

### Cycle 3 — retiring records when

```csharp
    [Fact]
    public void Retiring_a_copy_records_when_it_happened()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);
        DateTimeOffset when = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

        copy.Retire(when);

        Assert.Equal(when, copy.RetiredAt);
    }
```

```
error CS1501: No overload for method 'Retire' takes 1 arguments
```

```csharp
    public void Retire(DateTimeOffset retiredAt)
    {
        TransitionTo(CopyStatus.Retired,
            CopyStatus.Available, CopyStatus.InInspection, CopyStatus.InRepair);

        RetiredAt = retiredAt;
    }
```

**The ordering is the whole implementation.** `TransitionTo` throws before `RetiredAt` is touched,
so a refused retirement leaves the copy exactly as it was — no status change and no stray timestamp
on a copy that is still on the shelf.

**And the red here is wider than one test**, which is worth showing rather than skipping past:
`CopyStatusTests` has five `Retire()` call sites, and all five stop compiling the moment the
signature changes. Each needs an instant, and not one of those tests is about *when* — so they get
one shared value next to the helpers at the bottom of the file, for the same reason `Available()`
exists:

```csharp
    private static readonly DateTimeOffset AnyInstant =
        new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);
```

`copy.Retire(AnyInstant)` in all five places, and every one of them goes green again with no other
edit. Five tests that had to be *touched* and none that had to be *rethought* is the safety net
doing its job on the first day it was needed.

### The clock is a parameter, and that is a decision

The version everybody writes first:

```csharp
// The tempting version.
public void Retire()
{
    TransitionTo(CopyStatus.Retired, ...);
    RetiredAt = DateTimeOffset.UtcNow;
}
```

Shorter, and no caller has to be told what time it is. It also makes the cycle 3 test impossible to
write honestly: the only available assertions are *within a second or so of now*, which is a test
that passes on a fast machine and fails on a loaded CI runner, or *not null*, which asserts almost
nothing. **A test that cannot name the expected value is usually pointing at a hidden input**, and
`UtcNow` is exactly that — an argument the method reads from a global instead of accepting.

`TimeProvider` is the framework's answer to this and it is a good one: inject it, and tests supply a
`FakeTimeProvider` they control. It is not used here for one reason worth stating — it puts a
service into a domain object that currently has none. `Copy` has no dependencies at all, is
constructed by a static factory, and every test in the file arranges it in two lines. Adding a
constructor dependency so that one method can ask what time it is buys nothing over a parameter that
says the same thing more directly.

**The trigger for changing this is named:** the moment several domain operations need a consistent
"now" within one unit of work, passing the instant to each one starts to drift and `TimeProvider`
becomes the better shape. One operation is not that moment.

**And to correct episode 15 slightly**, which called `retired_at` "a database concern with a
database default": it is not. A `DEFAULT now()` on the column records **when the row was written**,
which is a different fact — it drifts by however long the transaction took, and it is simply wrong
if the retirement is ever recorded after the fact. The endpoint in episode 23 passes the instant in,
and the domain records what it was told.

---

## Step 6 — `CatalogDbContext`, and how much persistence gets to change the domain

`src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs`:

```csharp
using System.Reflection;

using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Copy> Copies => Set<Copy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
```

**Nine lines, and no mapping in any of them.** `OnModelCreating` is where mapping code accumulates in
most codebases — one entity is fine, seven is a four-hundred-line method that everybody scrolls past
— so the configuration classes go in from the start and this method only finds them.

**And the folder is in the API project**, which is what the plan says and is worth defending for one
paragraph rather than assumed. There is no `BrickShare.Catalog.Infrastructure`. Episode 2 set the
rule — *projects appear in this course when something forces them* — and episode 13 showed what
being forced looks like: six files with a genuinely different dependency profile from everything
around them. Two files that reference EF Core in a project that already references EF Core is not
that. **The trigger is named so the later extraction is a decision rather than a surprise:** when
something other than the API needs to reach the database — a background worker, the migration runner
in episode 19 if it stops being a `dotnet ef` invocation — the mapping has a second consumer and
earns its own project.

### What EF asked the domain to change, and what it did not

This is the part to slow down on, because "EF Core forces anaemic models" is a widely held belief
and the class from episode 15 is a fair test of it. `Copy` has a private constructor, a static
factory, no public setters at all, two get-only properties, an enum and a value object.

**Nothing about it changes.** Not one line. What EF does instead:

- **Materialises through the existing private constructor.** EF binds constructor parameters to
  properties by name, case-insensitively — `label` to `Label`, `grade` to `Grade` — and private
  constructors are eligible. The static `Register` factory is never called, which is correct: EF is
  reconstituting a copy that was already registered, not registering a new one, and running the
  registration logic again would assign a fresh `Id`.
- **Writes private setters and backing fields directly.** `Status` and `RetiredAt` have private
  setters; `Id` and `Label` have none at all. EF's default is to write the auto-property backing
  field, which is how a get-only property is populated without the domain opening a door for anyone
  else.

The standard fallback, if a type is ever shaped so that constructor binding cannot work, is a
private parameterless constructor for EF's use only. It is not needed here, and when it is needed it
is one line and a comment — a much smaller concession than the folklore suggests.

**The rule this step establishes, and it holds for the rest of the course:** the mapping bends to
the domain. When the two disagree, the configuration file is what changes. A domain type changes for
a mapper only when the mapper has found something genuinely wrong with it — which is a rarer event
than the amount of `[Column]` in the world suggests.

**Not test-driven.** This is wiring.

---

## Step 7 — `CopyConfiguration`, one line at a time

The heart of the episode.

`src/Catalog/BrickShare.Catalog.Api/Persistence/CopyConfiguration.cs`:

```csharp
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class CopyConfiguration : IEntityTypeConfiguration<Copy>
{
    public void Configure(EntityTypeBuilder<Copy> builder)
    {
        builder.ToTable("copies");

        builder.HasKey(copy => copy.Id);
        builder.Property(copy => copy.Id).HasColumnName("id");

        builder.Property(copy => copy.Label)
            .HasColumnName("label_code")
            .HasConversion(label => label.Value, value => LabelCode.Parse(value))
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(copy => copy.Label)
            .IsUnique()
            .HasDatabaseName("ix_copies_label_code");

        builder.Property(copy => copy.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(copy => copy.Grade)
            .HasColumnName("grade")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(copy => copy.RetiredAt)
            .HasColumnName("retired_at")
            .HasColumnType("timestamp with time zone");

        builder.UseXminAsConcurrencyToken();
    }
}
```

### Every table and column name is typed out

There is a package that would delete half of those lines — `EFCore.NamingConventions`, one call to
`UseSnakeCaseNamingConvention()`, and `RetiredAt` becomes `retired_at` for free everywhere.

**It is not taken, and the reason is the locked constraint at the top of this course:** *everything
explicit — nothing generated or hidden behind a scaffolder*. A student reading this file can see the
schema in it. With the convention, the schema is a function of C# property names and a third-party
package's rules, and a rename refactor in the domain silently becomes a column rename in a migration
— which is a **destructive** database operation triggered by a keystroke nobody thought of as
touching the database.

**Be fair about the cost, because it is real**: this is more lines, and it will be more lines again
with seven tables. The trade is deliberate and it can be reopened — a codebase with fifty entities
would probably take the package. A course teaching what the mapping does should not hide the
mapping.

### `HasConversion<string>()` — episode 15's promise, cashed

Episode 15 ended on this and it is the single most consequential line in the file:

> **`CopyStatus` must persist as its name, not its ordinal** — the enum was declared in the order
> `docs/IDEA.md` lists the states, this episode said out loud that reordering it changes no
> behaviour, and storing ordinals would make that a lie in the most expensive way available.

Do the failure on camera, because it is genuinely frightening. EF's default for an enum is its
underlying `int`. So `Available` is `0`, `Reserved` is `1`, `OnRent` is `2`. Now, six months from
now, somebody sorts the enum alphabetically — a tidy-up, a code review suggestion, an IDE action.

```csharp
public enum CopyStatus
{
    Available,          // 0
    AwaitingInspection, // 1  — was Reserved
    InInspection,       // 2  — was OnRent
    ...
}
```

**Count what fails.** `dotnet build`: nothing. `dotnet test`: nothing — episode 15 argued explicitly
that the order carries no meaning, and all twenty-one tests agree. `dotnet ef migrations add`: no
change, because the column is still `integer`. The pull request is three lines, all of them
alphabetical, and it is approved in a minute.

And every reserved copy in the shop is now awaiting inspection, and every copy out with a customer
is on an inspection bench. **There is no error, no exception and no log line** — the data did not
change, its meaning did, and every one of those states is perfectly plausible for a copy to be in.
The discovery is a staff member saying a box is missing.

Storing the name makes episode 15's claim true: the declaration order is free to change, because
nothing depends on it. `text` costs a few bytes per row over `integer`, the column is legible in
`psql`, and `WHERE status = 'OnRent'` in a support query says what it means.

**`ConditionGrade` gets the same treatment and the argument is stronger**, because that enum *is*
ordered — episode 14 built `IsBetterThan` on the fact that `New < Fair` — and an ordinal column
would tempt somebody into `WHERE grade <= 1` in a report, which is a comparison the domain sealed
inside one method precisely so it would never be written by hand.

The `HasMaxLength` is a guard rail on the same idea: the column holds a name from a known list, and
a `varchar(32)` says so where an unbounded `text` invites anything.

### `LabelCode` converts, and does not become a string

```csharp
            .HasConversion(label => label.Value, value => LabelCode.Parse(value))
```

Two lambdas: how the type goes to the database, and how it comes back. The property stays
`LabelCode` in the domain — episode 13's whole argument about primitive obsession survives the round
trip — and the column is a plain `varchar` that any SQL client can read.

**The `Parse` on the way back is doing something worth noticing.** It re-validates against the
regular expression on every read. A row hand-edited to `BRK-OOPS` throws when it is loaded rather
than flowing into the domain as an invalid `LabelCode`, which is the correct trade for a type whose
whole existence is a format guarantee. The alternative — a bypassing constructor that trusts the
database — is faster and is the right choice for a hot path with millions of rows. This is a few
thousand boxes.

### The unique constraint belongs to the database

```csharp
        builder.HasIndex(copy => copy.Label)
            .IsUnique()
```

`docs/IDEA.md` says two boxes may not carry the same label. The version of that rule most codebases
ship:

```csharp
// Not the rule.
if (await db.Copies.AnyAsync(c => c.Label == label))
{
    throw new InvalidOperationException("Label already used.");
}

db.Copies.Add(Copy.Register(label, grade));
await db.SaveChangesAsync();
```

**This is episode 15's race, in a new costume.** The check and the write are two operations with a
gap between them, and two staff registering stock at two counters can both pass the check before
either writes. The result is two boxes with one label, which is exactly the state the shop cannot
recover from: a scan is now ambiguous, and no amount of application code fixes rows that already
exist.

A unique index does not have that gap. The second `INSERT` fails inside the database, under whatever
concurrency the database is under, including from `psql`, from a migration script, and from the
support tool nobody remembered to put the check in. **It is the same principle episode 15 spent a
step on — put the enforcement where the data is — arriving as a schema line instead of a domain
guard.**

The application check is still worth having, in episode 23, for the message: a `409` explaining
which label is taken reads better than a wrapped `23505`. **But it is a nicety over a guarantee, and
that ordering matters** — a nicety implemented as if it were the guarantee is how databases end up
with duplicates.

**And the plan promised two constraints here, not one.** *Unique constraints on set number and label
code.* Only the label code lands, because there is no `catalog_sets` table yet — see the end of this
episode for why, and episode 22 for where it goes. The argument is the same one, twice.

### `timestamp with time zone`, never `timestamp`

```csharp
            .HasColumnType("timestamp with time zone")
```

Postgres has two timestamp types and one of them is a trap. `timestamp without time zone` stores the
digits somebody handed it and no offset, so the same value means a different instant depending on
who reads it — which surfaces the day the shop's server and a developer's laptop disagree, or twice
a year when the clocks change. `timestamptz` stores an instant, normalised to UTC.

Npgsql maps `DateTimeOffset` to `timestamptz` by default, so this line is a statement of intent
rather than a correction. **It also comes with a rule Npgsql enforces and it is better to meet it
here than in production:** a `DateTimeOffset` written to `timestamptz` must have a zero offset. A
value carrying `+02:00` throws on write. That is Npgsql refusing to guess, and it pushes the
conversion to the edge of the system where the caller knows what the local time meant — which is
where it belongs.

### `xmin` — episode 15's second debt

```csharp
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
```

The other bullet from episode 15's list:

> **No optimistic concurrency.** Two staff scanning the same box at once is not hypothetical, and
> the second write should fail loudly rather than overwrite the first — but a row version needs a
> row.

The failure without it is last-write-wins and it is silent. Two staff open the same copy. The first
sends it for repair. The second, working from a page loaded a minute ago, shelves it. Both writes
say `UPDATE copies SET status = ... WHERE id = ...`, both succeed, and the box is on the shelf with
a fault nobody recorded. **Nothing went wrong from the database's point of view** — the second
update was a perfectly legal statement — and this is why the problem is invisible until a customer
finds it.

With a concurrency token, EF adds the token to the `WHERE` clause and checks how many rows it
changed. Zero rows means somebody else got there first, and `DbUpdateConcurrencyException` is
thrown. Episode 20 builds the `ProblemDetails` mapping that turns a refusal into a `409`, and
episode 23 is where a copy write path exists to raise this one — a staff member reloads and sees the
repair rather than silently erasing it.

**`xmin` is the elegant part and it is Postgres-specific.** Every row already carries a hidden
system column holding the id of the transaction that last wrote it, maintained by the engine whether
anyone asks or not. Using it as the token costs **no column, no migration and no domain property** —
nothing appears on `Copy`, and the version is not something the domain has to remember to increment.

**Read those three lines as a description of a shape rather than a spell**, because that is what
they are, and the provider is checking for it. `IsRowVersion()` is shorthand for
`.ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()` — *the database writes this, and I will
compare it before I write* — and Npgsql's model-finalising convention looks for exactly that pair on
a property whose store type is `xid`, which is what a `uint` maps to. Finding it, the provider
points the property at the `xmin` column, and its migration generator refuses to emit a column for
it because system columns already exist and cannot be created twice.

**Worth knowing rather than memorising:** older versions of the provider had a
`UseXminAsConcurrencyToken()` helper that set all of this up in one call. It is gone, and the
replacement is the three lines above. Say so on camera, because a student searching for `xmin` and
EF Core will find the old helper in a great many blog posts and Stack Overflow answers that are
several major versions out of date.

**The same call means something different on another database, and that is the useful contrast.**
`IsRowVersion()` against SQL Server maps to a real `rowversion` column that appears in the table,
in the migration and in every `SELECT *`. Against Postgres it attaches to a column that was already
there. Same intent, same API, two very different schemas — which is a fair reminder that a provider
is not a thin translation layer.

### The first shadow property in this course

`Property<uint>("xmin")` names a property `Copy` does not have. EF calls that a **shadow property**:
it exists in the model, it is tracked, it is read and written on every round trip, and there is no
C# member anywhere to hold it.

**This is step 6's rule at its strongest.** The mapper genuinely needs somewhere to keep a version;
the domain has no business owning one, because "which transaction last wrote this row" is not a fact
about a LEGO box. A shadow property gives EF what it needs without `Copy` learning what a
concurrency token is — and the alternative is visible in Npgsql's own documentation, which shows a
public `uint Version` on the entity. That version works, and it would be the only property on `Copy`
that exists for the benefit of a library.

The cost is honest and worth naming: a shadow property cannot be read in C# without going through
`context.Entry(copy).Property<uint>("xmin")`, so it is invisible to anyone reading the domain class.
For a value nothing in the domain should ever read, invisible is the correct amount of visible.

**The limits, stated honestly, because this is the point in an episode where a student decides they
are now safe:** it protects updates made through this `DbContext`. Raw SQL, a migration script or a
`psql` session bypasses it entirely. It is optimistic — it detects a collision, it does not prevent
one, and something must handle the exception or the collision merely becomes a `500`. And nothing
here is proved: **the assertion that a second write fails needs two `DbContext`s and a real
database, which is episode 17.**

### The money convention, and a column that is not here yet

`CatalogDbContext`:

```csharp
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every Money property in this model, forever, is numeric(10,2). Registered centrally so
        // the correctness does not depend on remembering it in each configuration file.
        configurationBuilder.Properties<Money>()
            .HaveConversion<MoneyConverter>()
            .HaveColumnType("numeric(10,2)");
    }
```

`src/Catalog/BrickShare.Catalog.Api/Persistence/MoneyConverter.cs`:

```csharp
public sealed class MoneyConverter() : ValueConverter<Money, decimal>(
    money => money.Amount,
    amount => new Money(amount));
```

**And now the honest part, because there is no money column in this migration.** The plan promises
this episode teaches `numeric(10,2)` for money and *the mapping that guarantees it*, and `copies`
holds no money — rental price, daily rate and deposit are computed, never stored, which episode 12
drove out with tests, and the two prices that *are* stored belong to `catalog_sets`, which does not
exist yet.

So what lands is not a column. It is the thing that makes the first money column, in episode 22,
impossible to get wrong by forgetting — and *by forgetting* is the only way this goes wrong, which
is exactly why it is a convention rather than a line repeated in each configuration file.
`HasColumnType("numeric(10,2)")` written per property is correct for precisely as long as everybody
remembers, and there are two prices on `catalog_sets` alone.

**Be precise about what forgetting actually costs**, because the honest answer is smaller than the
usual scare story and still worth the line. Left alone, EF and Npgsql map a `decimal` to `numeric`
with **no precision and no scale** — which is exact arithmetic, not floating point, so the
horror-story drift does not happen. What is lost is the constraint: an unbounded `numeric` stores
`12.3456` without complaint, so *money has two decimal places* is guaranteed only by `Money`'s
constructor and by nothing in the schema. Give the column a scale and the database refuses to hold a
price the shop could never charge, whatever wrote it.

The `double precision` version is the real disaster, and it is one level up: it arrives when
somebody models money as `double` in C# in the first place, which is where episode 13 opened. That
episode fixed the C# side three episodes ago. **This line is the same argument, arriving as a column
type** — a `decimal` all the way through the application that lands in a `double precision` column
has every bit of the drift back, from the other end.

**Not test-driven, any of step 7.** A test asserting `"numeric(10,2)"` against a model that says
`"numeric(10,2)"` is a transcription. What proves this file is a database, and that is episode 17.

---

## Step 8 — The first migration, as a file somebody reads

```bash
dotnet ef migrations add InitialCatalog --project src/Catalog/BrickShare.Catalog.Api
```

Three files appear under `src/Catalog/BrickShare.Catalog.Api/Migrations/`, and the one to open on
camera is the timestamped one:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "copies",
        columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            label_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
            status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            grade = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_copies", x => x.id);
        });

    migrationBuilder.CreateIndex(
        name: "ix_copies_label_code",
        table: "copies",
        column: "label_code",
        unique: true);
}
```

**Read it line by line and check it against step 7**, because this is the moment "migrations
generated and committed as reviewable files" stops being a slogan. Every decision argued in the last
step is visible here as a consequence: `status` is `character varying(32)` and not `integer`,
`retired_at` is `timestamp with time zone`, the index is unique, and `xmin` appears nowhere — the
provider keeps a list of Postgres system columns and never emits a migration operation for one,
which is what makes the concurrency token free.

**Notice what a migration is: a file, in the repository, in a pull request.** It is not a state the
database drifts into. Someone can be asked to review it, and a reviewer who knows nothing about EF
can still read `CreateTable` and say "that should not be nullable". That is the entire argument for
generated-and-committed over an ORM that reconciles the schema at runtime.

**`Down` is not decoration.** It drops the table, and it exists because a deployment that has to be
rolled back at 2 a.m. is not the moment to be writing reverse SQL. Say the caveat too, since
students will meet it: `Down` restores the *schema*, never the data. A dropped column comes back
empty. Which is exactly why episode 19 teaches expand-then-contract — add before you remove — so
that a rollback almost never needs a `Down` at all.

**And the thing you do not do to this file: edit it by hand to change the design.** An unwanted
column means changing `CopyConfiguration`, deleting the migration and regenerating it. The generated
file is the output; the configuration is the source. (Hand-editing a migration is legitimate for
things the generator cannot know — a data backfill, a concurrent index build — and those go in on
purpose, with a comment saying why.)

### Generated code meets episode 10's gates

Episode 10 turned on `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and a `dotnet format
--verify-no-changes` step in CI. That gate has now met a file nobody typed, and the build or the
format check will have opinions about it.

The fix is **not** to hand-format generated code, which lasts until the next `migrations add`.
`.editorconfig` gets a scoped section:

```ini
# EF Core writes these files; nobody edits them, so style rules have no one to instruct. The
# analyzers stay on for every other file in the project, which is the point of scoping it here
# rather than lowering the bar globally.
[**/Migrations/*.cs]
generated_code = true
dotnet_analyzer_diagnostic.severity = none
```

**The distinction worth naming**, because "just turn the rule off" is a habit worth not teaching: a
style rule exists to instruct an author. Generated files have no author, so the rule has nobody to
talk to and turning it off costs nothing. That is a different act from silencing a rule because it
found something inconvenient in code somebody wrote — and the scoped section is what keeps the two
apart in a pull request.

### Applying it

```bash
docker compose up -d postgres
dotnet ef database update --project src/Catalog/BrickShare.Catalog.Api
```

**`dotnet ef` needs a working connection string at design time**, which is a real thing that trips
people: the tool builds the application's host to find the `DbContext`, so it reads
`appsettings.Development.json`, which means `ASPNETCORE_ENVIRONMENT` has to say `Development`
(`launchSettings.json` sets it for `dotnet run`, not for the tool — `--environment Development` or
the environment variable does it here). It also connects to `localhost`, not `postgres`, because the
tool is running on the host. Step 2, arriving as a practical consequence.

The command creates a second table, `__EFMigrationsHistory`, and it is worth pointing at: that is
how the database knows which migrations it has already applied, and it is why running the command
twice is safe.

---

## Step 9 — Wiring, and episode 3's promise finally paid

`src/Catalog/BrickShare.Catalog.Api/Program.cs`:

```csharp
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Catalog")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>(tags: ["ready"]);
```

and the comment episode 3 left behind gets corrected on camera. It reads:

```csharp
// Nothing is tagged yet — Postgres arrives in episode 15, Blob Storage in episode 25.
```

Both numbers are wrong, by one episode each, because the plan moved after the comment was written:

```csharp
// Postgres, as of this episode. Blob Storage joins it in episode 26.
```

**A comment that names a future episode is a comment that goes stale**, and this one is a small
demonstration of why the course plan is the source of truth for ordering and a code comment is not.
It was still worth writing: it told the next reader that the empty predicate was a shape and not an
oversight, which is the only job it had.

**Thirteen episodes of an empty shape, and this is the payoff.** Episode 3 built two health
endpoints with nothing to check and argued the difference between them anyway; today that difference
is demonstrable.
Stop the database and `/health/ready` goes unhealthy — App Service takes the instance out of
rotation and leaves the process alone — while `/health/live` stays healthy, because restarting a
process cannot fix a database that is down. Wire the restart probe to the dependency check and one
database blip restarts every instance in the service, which is how a small outage becomes a large
one. **That sentence was theory in episode 3. It is now something that can be shown in ten seconds.**

`AddDbContextCheck` calls `CanConnectAsync`, which is the right depth for readiness: it proves this
instance can reach its database, and it does not run a query that costs anything.

### And no `Database.Migrate()` at startup

The line that is deliberately absent:

```csharp
// Not this. Ever.
await app.Services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
```

It is in a great many tutorials and it works flawlessly with one instance, which is exactly what
makes it dangerous. With three instances, three processes start at once and race to alter the same
schema. `docs/architecture/catalog.md` puts it bluntly:

> This is the single most common way a first Azure deployment goes wrong, and it only shows up when
> you scale past one instance — which is exactly when you least want to discover it.

Migration is a **pipeline step**, running once before the new revision is released, and that is
episode 19. Locally it is a command a human runs, which is a feature: the schema changes when
somebody decides it does.

**Not test-driven.** Registration and configuration.

---

## Step 10 — Through the gates

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

```bash
docker compose up -d
docker compose ps          # postgres reports healthy; catalog-api waited for it
```

PR, green, merge, deployed.

**And the deployed app in Azure has no database**, which is fine and worth saying rather than
leaving as an uncomfortable silence. There is no Postgres in Terraform until episode 18, so App
Service supplies no `ConnectionStrings__Catalog`, the `DbContext` is registered with nothing to
connect to, and no code path asks it to. The app starts exactly as before, because a `DbContext` is
lazy: it opens a connection when something queries, and nothing does.

**So `/health/ready` in Azure reports unhealthy after this deploy, and that is correct.** An
instance that cannot reach its database should say so. Look at it on camera, and notice the thing
that keeps this survivable: `health_check_path` is not set on the App Service, so nothing acts on
that answer yet — the probe is wired up in episode 18, when there is a database for it to be right
about. Registering the check today and watching it be honestly unhealthy is a better lesson than
withholding it until it would pass.

---

## What this episode deliberately does not do

- **No `catalog_sets`, no `set_id`, no set-number constraint.** The domain has no `CatalogSet` type,
  and inventing one to satisfy a schema diagram is structure ahead of need — the argument episodes 2
  and 13 made about projects, arriving as a table. A `copies` table with no set to point at is an
  honest description of a domain that has no set in it. Episode 22 creates sets, and brings the
  table, the foreign key and the second unique constraint with the code that fills them. **This is
  what incremental migrations are for**, and it is why the second one being routine matters more
  than the first one being complete.
- **No checklist items, photographs, grade multipliers, Rebrickable snapshots or outbox.** All five
  are in the architecture's schema; none has code that writes to it. Photographs are episodes 26 and
  27, the checklist and snapshots are episode 22, and the outbox belongs to the messaging module by
  the course plan's staging rule — an outbox with no subscriber is a table nobody reads.
- **No `baseline_weight_grams`.** It is recorded at registration, which is episode 23, and it is a
  column with no domain property yet.
- **No repository, no unit of work.** `DbContext` is both — it tracks changes and commits them
  atomically — and wrapping it in an interface that exposes the same methods with different names
  buys nothing until something needs a second implementation. Episode 17's tests run against a real
  Postgres, so the usual reason for the abstraction, faking the database, is one this course
  deliberately does not want.
- **No query, no endpoint, nothing that reads a copy back.** Not one `SaveChanges` runs in this
  episode. Registration through HTTP is episode 23.
- **No test that touches a database** — episode 17, with Testcontainers, and it collects every claim
  step 7 made.
- **No Postgres in Azure** (18) and **no migration in the pipeline** (19).
- **No Dapper, no second data-access story, at any point.** The plan settles this: EF for reads and
  writes both, and raw SQL runs through EF on the day something genuinely needs it.

## Verification

```bash
docker compose up -d
docker compose ps                  # postgres healthy, catalog-api running

dotnet build                       # 0 warnings, 0 errors
dotnet test                        # green, three new tests
dotnet format --verify-no-changes  # exits 0

dotnet ef database update --project src/Catalog/BrickShare.Catalog.Api
docker compose exec postgres psql -U brickshare -d brickshare_catalog -c '\d+ copies'
```

The `\d+` output is the actual verification of this episode, and it should be read on camera against
step 7: `id uuid` primary key, `label_code character varying(10) not null` with a unique index,
`status` and `grade` as `character varying` rather than `integer`, `retired_at timestamp with time
zone` and nullable.

Then three deliberate breakages. **There are no mapping tests to turn red, which is the point** —
each of these is caught by something else, and knowing which tool catches which mistake is worth as
much as the mapping itself:

1. **Delete `.HasConversion<string>()` from `Status` and run `dotnet ef migrations add Whatever`.**
   Nothing fails. `dotnet build` is clean, `dotnet test` is green, and the only evidence anywhere is
   one line in the generated file: `status = table.Column<int>(type: "integer", ...)`. **Read that
   line out and then delete the migration.** The reviewable file is the only thing standing between
   this repository and step 7's silent catastrophe, which is a fair summary of why migrations are
   committed rather than applied.
2. **Insert two rows with the same label code, in `psql`, with the API not running.**

   ```sql
   INSERT INTO copies (id, label_code, status, grade)
   VALUES (gen_random_uuid(), 'BRK-7F3K2Q', 'Available', 'New');
   ```

   Run it twice. The second fails with `duplicate key value violates unique constraint
   "ix_copies_label_code"` — enforced with no application in the picture at all, which is the
   difference between a constraint and a convention.
3. **`docker compose stop postgres`, then call both health endpoints.** `/health/ready` returns 503;
   `/health/live` returns 200. Start it again and readiness recovers without a restart. Episode 3's
   argument, demonstrated against a real dependency for the first time.

## Next

[Episode 17 — Integration tests against a real database](catalog-api.md#episode-17--integration-tests-against-a-real-database):
Testcontainers starts a Postgres per test run, and the mapping written today finally gets asserted rather than
described.

**Everything step 7 claimed is currently unproven**, and the two most expensive claims are exactly
the two nothing smaller than a real database can check: that the unique index refuses a duplicate
label through the application, and that a second concurrent write raises
`DbUpdateConcurrencyException` instead of overwriting the first. Both need two connections and a
real engine.

That is also where the in-memory provider and SQLite get taken apart, and this episode has just
supplied the argument in advance: **a fake database has no `numeric(10,2)`, no `timestamptz`, no
`xmin`, and enforces no constraint the mapping just spent a step putting in the schema.** Every
decision made today would pass a test against a fake, whether or not it was right.
