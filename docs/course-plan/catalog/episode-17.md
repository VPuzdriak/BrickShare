# Episode 17 — Integration tests against a real database

← [Course plan](catalog-api.md) · Previous: [Episode 16 — Postgres in Compose, EF Core mapping](episode-16.md)

Episode 16 wrote a mapping file and argued every line of it. Not one of those arguments was checked
by anything. The build was green, sixty-nine tests were green, and a `CopyConfiguration` with
`.IsUnique()` deleted would have been just as green — which is a fair description of a codebase with
no test that has ever opened a connection.

This episode gives the test suite a real Postgres. It starts when `dotnet test` starts, it dies when
the run ends, it is created by the same migration that will run in the pipeline, and it is empty at
the beginning of every test. Then two of episode 16's claims get collected.

The part worth the episode is not Testcontainers, which is four lines. It is **why a fake database
is worse than no database**, and the reason is sharper than the one usually given: a fake does not
merely fail differently — **it changes which tests you write**.

**Done when** `dotnet test` starts a Postgres container, applies the migration, resets the database
before every test, and proves the two things episode 16 could not: that the unique index refuses a
duplicate label code through the application, and that the second of two concurrent writes is
refused instead of silently winning. And `/health/ready` returns 200 for the first time in fourteen
episodes.

## Before recording

- Episode 16 merged: `CatalogDbContext`, `CopyConfiguration`, the `InitialCatalog` migration, and
  `dotnet test` green.
- Docker running.
- A branch.
- [`episode-16.md`](episode-16.md) open in two places: **step 7**, which is the file this episode
  tests, and its closing **Next** section, which is the debt this episode collects.
- [`catalog-api.md`](catalog-api.md) open at the Episode 17 entry — its first sentence is the whole
  lesson and it is worth reading out before writing anything.

**This episode is not TDD, and it says so rather than performing it.** The code under test was
written in episode 16 and is sitting in `main`. CLAUDE.md draws the line these tests fall on:

> When a test passes the moment it is written, keep it and say so: some tests drive a design and
> some describe a rule. Both belong in the suite, and only the first is TDD doing its job.

The fixture in steps 3 to 5 is infrastructure and is exempt for the usual reason. The four tests in
steps 6 to 9 describe rules that already exist.

**But every one of them still gets a red on camera**, produced by deleting the line in
`CopyConfiguration` that the test exists to protect. That is not TDD — the design is not being
driven anywhere — and it is the honest substitute: **a test you have never seen fail is a test you
have not finished writing.** It is worth doing for each of the four, because for two of them the
deletion is genuinely invisible everywhere else in the toolchain.

---

## Step 0 — Sixty-nine green tests that have never opened a connection

Before any code, read episode 16's last paragraphs out loud:

> **Everything step 7 claimed is currently unproven**, and the two most expensive claims are exactly
> the two nothing smaller than a real database can check: that the unique index refuses a duplicate
> label through the application, and that a second concurrent write raises
> `DbUpdateConcurrencyException` instead of overwriting the first. Both need two connections and a
> real engine.

Stop on **why** those two are the expensive ones, because it is not that they are complicated.

A unique index is not a property of a `Copy`. It is a property of *the table*, enforced by *the
engine*, at the moment two `INSERT`s meet. A concurrency token is not a property of a `Copy` either
— `Copy` has no version field, deliberately, because episode 16 put it in a shadow property. It is a
property of *a row being written twice*.

Neither of those facts exists inside a single process with no database in it. There is no clever
unit test that gets at them, no mock that would be telling you anything, and no amount of reading
`CopyConfiguration.cs` that constitutes proof. **The only instrument that can observe them is a
Postgres, and there has to be one inside `dotnet test`.**

**This step is not code.**

---

## Step 1 — Why not a fake database

The reflex, when somebody says "the tests need a database", is to reach for something that is not
one. Both candidates deserve to be taken seriously on camera and rejected for a stated reason,
because "just use Testcontainers" is advice, and the argument is the lesson.

### `Microsoft.EntityFrameworkCore.InMemory`

It is a dictionary with a LINQ provider in front of it. It is not a relational database and does not
claim to be — **the EF Core team's own documentation discourages using it for this**, which is an
unusual thing for a team to say about their own package and worth quoting to a class.

Against this repository, specifically, it has: no SQL, so nothing can be asserted about what was
actually stored; no constraints, so `ix_copies_label_code` does not exist; no column types, so
`character varying(32)` versus `integer` is not a distinction it can make; no `numeric(10,2)`; no
`timestamptz`; no `xmin`. **Every single decision argued in episode 16 step 7 is invisible to it.**

### SQLite

The more seductive of the two, and the one that catches experienced people, because SQLite is a real
database. It has SQL. It enforces `UNIQUE`. Run in-memory mode and it is nearly as fast as the fake.
So the duplicate-label test would pass — genuinely, on a real engine.

And then:

- **No `timestamp with time zone`.** SQLite has no date type at all; it stores text or a number, and
  the offset rule Npgsql enforces does not exist.
- **`decimal` is not exact.** EF warns about this on every model with a `decimal` in it, because
  SQLite's storage classes have no fixed-point type. Episode 13's entire opening argument evaporates.
- **Different collation.** SQLite's `LIKE` is case-insensitive for ASCII by default; Postgres's is
  not. That is a test that passes and a search endpoint that does not, and episode 24 is where it
  would be discovered.
- **No `xmin`**, so claim two is untestable.
- **And the decisive one: the migration will not run.** `20260821222411_InitialCatalog` was generated
  by the Npgsql provider. Point SQLite at it and it fails outright. So the schema under test would
  have to be built by `EnsureCreated` from the model — meaning **the artifact that ships to
  production is not the artifact the tests ran against**, which is the one thing an integration test
  was supposed to be for.

### The argument that is better than the usual one

The plan states the headline this way:

> **A fake database gives you green tests and a broken production deploy**, which is worse than no
> tests, because it is confidently wrong.

True, and there is a sharper version underneath it. Look at what actually happens to a team that
starts on the in-memory provider: **they never write the duplicate-label test at all.** It cannot
pass, so it never gets written, so the rule needs enforcing somewhere the fake can see it — and they
write the check episode 16 warned about:

```csharp
// Not the rule. Episode 16, step 7.
if (await db.Copies.AnyAsync(c => c.Label == label))
{
    throw new InvalidOperationException("Label already used.");
}
```

That is green against the fake, green against Postgres, green in code review, and it is a race that
puts two boxes in the shop under one label. **The fake did not break a test. It removed the pressure
that would have produced a constraint**, and the test suite came out looking excellent.

*The choice of test infrastructure decides which bugs are expressible.* That is the transferable
sentence, and it is worth writing on the slide.

---

## Step 2 — Two packages, and one that is deliberately absent

`Directory.Packages.props`:

```xml
    <PackageVersion Include="Respawn" Version="7.0.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.14.0" />
```

`tests/BrickShare.Catalog.IntegrationTests/BrickShare.Catalog.IntegrationTests.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Respawn" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
```

**`Testcontainers.PostgreSql`, not `Testcontainers`.** The base package can run any image; the module
knows what a Postgres is — the default port, the environment variables it needs, and a readiness
probe that waits for the engine to accept connections rather than for the process to exist. That
last one is the same distinction Compose made in episode 16 with `condition: service_healthy`,
arriving as a library instead of a YAML key.

**And a package that is deliberately not added: `Npgsql`.** Step 7 uses `PostgresException` and
`PostgresErrorCodes` directly, and both are available already — they arrive transitively through the
project reference to the API, which references `Npgsql.EntityFrameworkCore.PostgreSQL`, which
references the driver. Adding a direct `PackageVersion` for `Npgsql` would pin a second version
number that has to be kept aligned with whatever version the provider wants, and getting that wrong
produces a load-time failure that reads like nothing in particular. **Take the version the provider
chose.**

**Not test-driven.** Package references.

---

## Step 3 — One container for the whole run

`tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs`:

```csharp
using BrickShare.Catalog.Api.Persistence;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace BrickShare.Catalog.IntegrationTests;

/// <summary>
/// The Postgres every integration test runs against: one container for the whole test run,
/// created by the same migration that will run in the pipeline, and reset before each test.
/// </summary>
public sealed class CatalogDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("brickshare_catalog")
        .WithUsername("brickshare")
        .WithPassword("brickshare")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using CatalogDbContext context = NewDbContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// A new context on the same database. Tests that need to see a row the way another
    /// connection would ask for two of these rather than clearing a change tracker.
    /// </summary>
    public CatalogDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
```

Four things in there are decisions rather than boilerplate.

### `postgres:18`, and the tag that has to stay in three places

The same tag as `docker-compose.yml`, pinned for the same reason episode 16 pinned it: an engine
that changes underneath the project on the day an image is rebuilt is the `global.json` problem
again.

But there is a second reason here that Compose did not have. **The tests exist to tell you what the
engine will do**, and an engine one major version away from the one in Azure is a different oracle.
So this tag, the Compose tag, and the `postgres_version` that episode 18 puts in Terraform are one
number written three times, and they are allowed to drift by exactly zero. Say the maintenance cost
out loud rather than discovering it in episode 18.

`PostgreSqlBuilder` has a parameterless constructor that supplies a default image, and it is not
this one. **Name the image every time** — the default is whatever the module's authors pinned, it
changes when the package is upgraded, and it is not visible anywhere in your repository. Passing it
to the constructor rather than calling `.WithImage(...)` afterwards is the same decision written more
tersely; both work, and the constructor puts the version on the first line where it cannot be skimmed
past.

### `MigrateAsync`, not `EnsureCreatedAsync`

The shorter call exists and would work:

```csharp
// Not this.
await context.Database.EnsureCreatedAsync();
```

`EnsureCreated` builds the schema straight from the model and **skips the migrations entirely** —
`__EFMigrationsHistory` is never created, and the database ends up in a state no `dotnet ef` command
can then manage. The tests would be running against a schema that no deployment has ever produced.

`MigrateAsync` runs `20260821222411_InitialCatalog`, the actual file, the one episode 16 read line by
line and the one episode 19 will run as a pipeline step. **The migration is therefore under test for
free, on every run, from today** — and that is a bigger deal than it sounds, because a migration that
does not match its configuration is otherwise noticed the first time it meets a real environment.

### Yes, this is `Database.Migrate()` at startup

Episode 16 was emphatic:

> ```csharp
> // Not this. Ever.
> await app.Services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
> ```

And here it is, at startup, in the fixture. **Say so on camera rather than hoping nobody notices**,
because a student who spots it and is not given the answer learns that the rules are decorative.

The prohibition has a reason, and the reason is the whole rule: *many application instances race to
alter one schema, and the failure is intermittent and only appears once you scale*. This is one test
process, running once, before any test runs, against a database that was created three lines ago and
that nothing else can reach. **There is no second writer, so there is no race.**

This is what a rule with its reason attached buys you: it survives meeting its own exception, and
you can tell the difference between an exception and a violation. A rule memorised as a slogan
cannot do either.

### The gotcha that costs everybody twenty minutes

The obvious next move is to make the fixture *be* the `WebApplicationFactory`. It does not compile:

```csharp
// Does not compile.
public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
```

`WebApplicationFactory` already has `ValueTask DisposeAsync()` from `IAsyncDisposable`. xUnit's
`IAsyncLifetime` wants `Task DisposeAsync()`. Same name, same parameters, different return type —
which C# does not allow, and the error message points at neither of the two things you were
thinking about.

It can be forced with an explicit interface implementation (`Task IAsyncLifetime.DisposeAsync()`),
and the result is a class with two dispose methods that behave differently depending on how it is
held. **The design here sidesteps it**: the fixture owns the container, and in step 9 it will own the
factory too, and the factory implements no lifecycle interface at all.

**Also note the xUnit version.** This repository is on `xunit` 2.9.3, where `IAsyncLifetime` returns
`Task`. xUnit v3 changed it to `ValueTask`. Every fixture sample on the internet is one or the other
and almost none of them say which.

**Not test-driven.** Infrastructure.

---

## Step 4 — The collection, and why parallelism is the isolation problem

`tests/BrickShare.Catalog.IntegrationTests/DatabaseCollection.cs`:

```csharp
namespace BrickShare.Catalog.IntegrationTests;

/// <summary>
/// Every test that touches Postgres joins this collection. It shares one container — and,
/// just as importantly, it stops the classes running at the same time as each other.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<CatalogDatabase>
{
    public const string Name = "catalog database";
}
```

**The second sentence of that comment is the step.** Everybody reaches for a collection fixture to
*share* the expensive thing, and that is the obvious half. The half that is not obvious:

xUnit runs **test classes in parallel by default**. One shared database, plus a reset between tests,
plus two classes running at once, is a reset deleting the rows a test in another class had just
inserted and was about to assert on. The failure is intermittent, moves when you add a test, and
looks exactly like a flaky database.

A collection is xUnit's unit of serialisation: classes inside one collection run one after another.
So the same attribute that shares the container is also what makes the reset safe, and **the fixture
and the isolation strategy are not two decisions — they are one.**

### The blunt alternative, and why not

```csharp
// Not this.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

It works, and it is a sledgehammer: the unit tests are in a different assembly, but everything in
*this* one now waits, including tests that never touch a connection. Which brings up the deliberate
omission:

**`LivenessTests` stays out of the collection.** It was written in episode 4 and it does not change
today. `/health/live` runs no checks, touches no dependency, and needs no database — that was the
entire argument of episode 3, and leaving this class outside the collection is that argument made
visible in the test project's structure. It keeps running in parallel with everything else, because
there is nothing for it to collide with.

Now the base class every database test derives from:

```csharp
namespace BrickShare.Catalog.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTest(CatalogDatabase database) : IAsyncLifetime
{
    protected CatalogDatabase Database { get; } = database;

    // Reset before, not after. See step 5.
    public Task InitializeAsync() => Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
```

**Not test-driven.** Test infrastructure — and worth naming, because "write a test for your test
base class" is a thought students do have.

### And one more thing about that name

`DatabaseCollection` does not compile cleanly, and the detour is worth taking, because it is the only
place in this episode where a tool tells you to make the code worse.

Paste the class in, build, and episode 10's gates fire:

```
error CA1711: Rename type name DatabaseCollection so that it does not end in 'Collection'
```

**Name which gate caught it, because two of them could have.** This is **CA1711**, from
`Microsoft.CodeAnalysis.NetAnalyzers`, which ships inside the SDK and is switched on by the
`AnalysisMode=Recommended` line episode 10 put in `Directory.Build.props` — then turned from a
warning into a build failure by `TreatWarningsAsErrors` on the line above it. It is **not**
SonarAnalyzer; that package has no reserved-suffix rule at all. Knowing which of your tools found
something is worth as much as fixing it, and by this point in the course there are four candidates.

**And the rule is right**, in general and emphatically. A type called `OrderCollection` that you
cannot `foreach` is a genuine trap: `Collection` is a reserved suffix precisely because a reader who
sees it stops reading and assumes `IEnumerable`. In a library that ships to other people, CA1711 is
protecting a stranger from an hour of confusion.

### The wrong fix, which is the interesting part

The reflex is to make the message go away by renaming:

```csharp
// Analyzer happy. Reader wrong.
public sealed class DatabaseCollectionFixture : ICollectionFixture<CatalogDatabase>
```

It compiles, the build goes green, and **the name is now a lie**. This class is not the fixture. The
fixture is `CatalogDatabase` — the thing that owns the container and gets injected into every test.
xUnit has three separate concepts in play and that rename collapses two of them:

| Concept | Type |
| --- | --- |
| The **fixture** — the shared object | `CatalogDatabase` |
| The **collection definition** — declares which fixture a group of classes shares | this class |
| The **collection name** — the string key that ties them together | `"catalog database"` |

Somebody looking for the fixture now opens `DatabaseCollectionFixture.cs`, finds nine lines and no
container, and has to go looking again. **An analyzer objection answered with a less accurate name is
a bad trade** — and it is a trade people make constantly, because the analyzer complains out loud and
the future reader does not.

`DatabaseCollection` is also the name in xUnit's own *Shared Context between Tests* documentation, so
keeping it means this repository reads like every xUnit example a student will find. That is worth
something on its own.

### So: suppress it, with the reason, where it applies

`.editorconfig` already has a section for this, added when test method names started using
underscores:

```ini
#### Test code ####
[tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none

# CA1711 reserves the "Collection" suffix for types that implement IEnumerable. A collection
# *definition* is xUnit's own term for a marker class that is never instantiated and never
# enumerated, and DatabaseCollection is the name in xUnit's documentation. Same reason as CA1707
# above: the CA17xx naming rules govern a published library's API surface, and a test assembly
# has none.
dotnet_diagnostic.CA1711.severity = none
```

**The scope is the argument.** Both rules are about what a *library consumer* sees, and a test
project ships to nobody — there is no consumer to mislead. Under `src/`, CA1711 stays on, and a
`FooCollection` that is not enumerable still fails the build there. A global suppression would have
bought the same green build and given away the protection everywhere it was actually doing something.

**This is now the third suppression in the course, and the third is where the pattern is worth
naming:** `#pragma warning disable S1118` on `Program.cs` (a rule that cannot be satisfied by a type
`WebApplicationFactory` must be able to construct), the `[**/Migrations/*.cs]` section in episode 16
(style rules with no author to instruct), and this one. Every one is scoped as narrowly as it can be
and carries a comment saying why.

*A suppression with a reason is a decision. A suppression without one is a shrug*, and the difference
is visible in a pull request, which is the only place it matters.

---

## Step 5 — Respawn, and resetting *before* rather than after

`ResetAsync` is the method step 4 just called and step 3 has not written. It goes on the fixture:

```csharp
using Npgsql;

using Respawn;
```

```csharp
    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using CatalogDbContext context = NewDbContext();
        await context.Database.MigrateAsync();

        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],

            // Clear this and the database forgets it has a schema, so the next `dotnet ef`
            // command tries to apply InitialCatalog to a database that already has the table.
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    /// <summary>
    /// Empties every table, in an order the foreign keys allow. Called before each test.
    /// </summary>
    public async Task ResetAsync()
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);
    }
```

### Four things worth a sentence

**`Respawner.CreateAsync` runs after `MigrateAsync`, and the order is not stylistic.** Respawn
inspects the database at creation time to build the foreign-key graph it will delete along. Create it
first and it finds no tables, resets nothing, and every test after the first inherits the previous
one's rows — which presents as an assertion failure in a test that is individually green.

**`SchemasToInclude = ["public"]`** is narrower than the default and says what it means. There is one
schema in this database, and naming it means a future extension schema — `pg_trgm` in episode 24
installs into one — is opted into rather than swept.

**Read the package rather than trusting it.** Respawn's Postgres adapter issues a single
`TRUNCATE … CASCADE`; its SQL Server adapter issues ordered `DELETE`s. Same API call, materially
different SQL, and the difference shows up in whether identity sequences restart. This is a
reasonable thing to spend ninety seconds on in a course about dependencies: **the interface is not
the behaviour**, and Respawn is small enough that reading it is a realistic suggestion rather than an
aspiration.

**Reset at the *start* of each test, not the end.** They sound equivalent and are not:

- A test that fails or crashes mid-way leaves its rows in the database, and you can look at them.
  Reset-after wipes the evidence as part of failing.
- Reset-before is also correct on the *first* test of the run, without depending on the container
  having been fresh — which stops mattering the day somebody enables container reuse.
- And it removes an entire ordering question: no test ever depends on its predecessor having cleaned
  up properly, because no test relies on anybody else's cleanup at all.

### The three strategies that were not chosen

This was the closest call in the episode and it deserves the table.

| Strategy | Why not here |
| --- | --- |
| **A transaction per test, rolled back** | The fastest option, and genuinely tempting — no deletes at all. It breaks as soon as the code under test opens its own transaction, which episode 23's batch registration does by design. And it does not reach across connections, so the moment a test goes through `WebApplicationFactory` (step 9) the app is on a different connection and sees none of the test's setup. |
| **A database per test class** | Perfect isolation, and the classes could run in parallel again. It pays the migration run once per class, and that bill grows with every migration this course adds between here and episode 30 — the cost lands later, on the people least able to see where it came from. |
| **A hand-written `TRUNCATE`** | About ten lines, no package, and correct today. |

**The last one is the close call, and it should be said out loud rather than waved past**, because a
locked constraint at the top of this course is *everything explicit — nothing generated or hidden
behind a scaffolder*, and a ten-line `ResetAsync` that truncates every table in
`context.Model.GetEntityTypes()` is fully readable in the file it lives in. That is a real argument
and it very nearly wins.

What decides it is the schema this repository is about to grow. Episode 22 brings `catalog_sets` and
a foreign key from `copies`; episodes 26 and 27 bring `photographs`; the messaging module brings an
outbox. From that point, "empty every table" is a graph-ordering problem, and the hand-written
version either grows a topological sort or grows a `CASCADE` that is quietly deleting more than the
author checked. **Respawn is the ten lines, already written, already handling the case that arrives
in five episodes.**

**And that is the fair shape of the trade-off, not a rule about packages.** Explicit wins when the
explicit version stays small. Here it does not.

**Not test-driven.** Test infrastructure.

---

## Step 6 — The harness proves itself

`tests/BrickShare.Catalog.IntegrationTests/Persistence/CopyPersistenceTests.cs`:

```csharp
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Persistence;

public class CopyPersistenceTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_registered_copy_comes_back_as_the_copy_that_was_registered()
    {
        Copy registered = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good);

        await using (CatalogDbContext writing = Database.NewDbContext())
        {
            writing.Copies.Add(registered);
            await writing.SaveChangesAsync();
        }

        await using CatalogDbContext reading = Database.NewDbContext();
        Copy read = await reading.Copies.SingleAsync();

        Assert.Equal(registered.Id, read.Id);
        Assert.Equal(LabelCode.Parse("BRK-7F3K2Q"), read.Label);
        Assert.Equal(ConditionGrade.Good, read.Grade);
        Assert.Equal(CopyStatus.Available, read.Status);
        Assert.Null(read.RetiredAt);
    }
}
```

**Two contexts, not one with a cleared change tracker.** `ChangeTracker.Clear()` would also force a
query, and it leaves the same connection, the same transaction scope and the same second-level
nothing in play. A second `DbContext` is the closest thing in-process to *somebody else asking* —
which is what "it was persisted" actually means.

### The assertion doing the most work is the first one

`Assert.Equal(registered.Id, read.Id)` looks like the throwaway line and is the one to stop on.

Episode 16 step 6 made a claim about how EF reconstitutes this type:

> **Materialises through the existing private constructor.** EF binds constructor parameters to
> properties by name, case-insensitively — `label` to `Label`, `grade` to `Grade` — and private
> constructors are eligible. The static `Register` factory is never called, which is correct: EF is
> reconstituting a copy that was already registered, not registering a new one, and running the
> registration logic again would assign a fresh `Id`.

That is a paragraph of confident prose about a library's internals, and this single comparison is its
proof. `Copy` assigns `Guid.CreateVersion7()` in the constructor. If EF had gone anywhere near
`Register`, or had constructed the object and then re-run the constructor body, `read.Id` would be a
brand-new GUID and this line would fail.

The rest of the assertions collect the other half of the claim quietly: `Id` and `Label` are
**get-only properties with no setter at all**, and they came back populated, which is EF writing the
auto-property backing field directly — the mechanism that let episode 15's domain class survive
episode 16 without one line changing.

### The red

Comment out `writing.Copies.Add(registered)` and watch `SingleAsync` throw. It is a cheap red and it
is the one that proves the *fixture* works — that the container came up, the migration ran, the
reset left an empty table, and the connection string reached it. Every later red in this episode
assumes all four.

---

## Step 7 — The unique index refuses a duplicate label

Episode 16's first debt.

```csharp
using Npgsql;
```

```csharp
    [Fact]
    public async Task Two_copies_cannot_carry_the_same_label_code()
    {
        LabelCode label = LabelCode.Parse("BRK-7F3K2Q");

        await using CatalogDbContext context = Database.NewDbContext();
        context.Copies.Add(Copy.Register(label, ConditionGrade.New));
        context.Copies.Add(Copy.Register(label, ConditionGrade.Good));

        DbUpdateException error =
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        PostgresException postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_copies_label_code", postgres.ConstraintName);
    }
```

### Assert on the error code, not the message

`PostgresErrorCodes.UniqueViolation` is the constant for `23505`, which is in the SQL standard's
class 23 and in the Postgres documentation, and it will still mean that in ten years. The message —
`duplicate key value violates unique constraint "ix_copies_label_code"` — is Npgsql relaying a
server string, it is localisable, and asserting on it makes the test fail on a driver upgrade that
changed nothing. **Use the constant; it also makes the test read as the rule** rather than as a
string comparison.

`ConstraintName` is the second assertion and it earns its place: without it, the test passes if
*any* unique constraint is violated. Naming the index in episode 16 (`.HasDatabaseName(...)`) is what
makes that assertion possible, and this is where that line gets paid for.

### The red, by removal — and the migration it takes to get there

This is the one red in the episode that costs more than a keystroke, because **the test runs against
a schema built by migrations**, which is exactly what step 3 chose. Editing `CopyConfiguration` alone
changes nothing the test can see: `MigrateAsync` replays `InitialCatalog`, which still creates a
unique index, and the test stays green off a file that no longer asks for one.

So the demonstration is four commands, and the fact that it takes four is itself the lesson.

**1. Remove the uniqueness** in
`src/Catalog/BrickShare.Catalog.Api/Persistence/CopyConfiguration.cs`:

```csharp
        builder.HasIndex(copy => copy.Label)
            .HasDatabaseName("ix_copies_label_code");
```

**2. Generate the migration that carries it:**

```bash
dotnet ef migrations add DropLabelUniqueness --project src/Catalog/BrickShare.Catalog.Api
```

**`migrations add` does not connect to anything.** It builds the model, diffs it against
`CatalogDbContextModelSnapshot.cs`, and writes files — so unlike episode 16's `database update`,
Postgres does not have to be running and the connection string does not have to be right. Worth
saying, because "do I need the database up for this?" is a real question with two different answers
for two commands that look like a pair.

Open the generated file. It is two operations, and it is the entire visible trace of the change:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropIndex(
        name: "ix_copies_label_code",
        table: "copies");

    migrationBuilder.CreateIndex(
        name: "ix_copies_label_code",
        table: "copies",
        column: "label_code");
}
```

Drop the unique index, create a non-unique one with the same name. **Read `unique: true` being
absent out loud** — that is the whole diff, and it is a line that is *not there*, which is the
hardest kind of thing to catch in a review.

**3. Run the tests:**

```bash
dotnet test
```

`Two_copies_cannot_carry_the_same_label_code` fails: `SaveChangesAsync` succeeds, `ThrowsAsync` finds
no exception, and there are now two boxes in the shop under one label. Nothing else in the suite
moves.

Notice what did **not** have to happen: no `dotnet ef database update`, and the Compose database on
`localhost:5432` was never touched. The container Testcontainers started applied both migrations to
an empty database and threw it away. That is step 3's choice paying off in a way that is easy to miss
— **the tests carry their own schema**, so a schema experiment costs a test run rather than a local
database somebody has to remember to put back.

**Then read what else noticed**, which is the frightening part and the same shape as episode 16's
enum demonstration: `dotnet build` — clean. `dotnet format` — clean. The analyzers — nothing. The
sixty-nine unit tests — green. **One test in the entire repository has an opinion about this line**,
and before today there were none. The migration file is the only other artifact that mentions it,
and it says so by omission.

### Rolling it back

**4. Undo, in the reverse order:**

```bash
dotnet ef migrations remove --project src/Catalog/BrickShare.Catalog.Api
```

`migrations remove` deletes the three files of the *last* migration **and reverts the model snapshot**
— which is the half people forget when they delete the files by hand and then spend twenty minutes
wondering why the next `migrations add` produces an empty `Up`. `CatalogDbContextModelSnapshot.cs` is
the record of what the previous migration left behind, and it is not optional bookkeeping.

Then put `.IsUnique()` back:

```csharp
        builder.HasIndex(copy => copy.Label)
            .IsUnique()
            .HasDatabaseName("ix_copies_label_code");
```

and confirm you are exactly where you started:

```bash
dotnet ef migrations list --project src/Catalog/BrickShare.Catalog.Api   # InitialCatalog, and nothing else
git status                                                              # clean
dotnet test                                                             # green
```

**The order matters, and it is the opposite of the way in.** `migrations remove` diffs the model
against the snapshot to decide what it is undoing, so running it while the configuration still says
what the migration says is the sane path. Restore `.IsUnique()` first and the tool is being asked to
remove a migration that no longer matches the code that produced it — which sometimes works and is
not a habit worth acquiring on camera.

### If you ran `database update` as well

Everything above assumes the throwaway migration was never applied to a real database, which is the
happy path here because the tests bring their own. If curiosity got the better of you and you applied
it to the Compose database, `migrations remove` refuses — it will not delete a migration the database
says it has applied. Two ways back:

```bash
# Precise: roll the local database back to the previous migration first.
dotnet ef database update InitialCatalog --project src/Catalog/BrickShare.Catalog.Api
dotnet ef migrations remove --project src/Catalog/BrickShare.Catalog.Api
```

```bash
# Blunt, and completely fine for a local development database with nothing in it.
docker compose down -v
docker compose up -d postgres
dotnet ef database update --project src/Catalog/BrickShare.Catalog.Api
```

**Both are worth showing, because the choice between them is the one that matters in production and
does not matter at all here.** `down -v` deletes the volume and every row in it. On a laptop that is
a five-second reset of data anybody can regenerate; against anything with a customer in it, it is the
end of your afternoon. Naming which environment you are in *before* reaching for a command is the
habit, and a local database is the only safe place to practise it.

`dotnet ef database update <TargetMigration>` is also the first appearance of something episode 19
will need: migrations run **forward to a named point**, and can be run backward to one. That is what
makes a rollback a command rather than an improvisation — and it is also why episode 19 teaches
expand-then-contract, so that the backward command is almost never the one you need.

### And the connection back to step 1

This is precisely the test that a team on the in-memory provider does not have. Not because they
chose badly under pressure, but because the test cannot be written against a fake, so the rule has to
live in application code — and the application-code version is the race episode 16 opened step 7
with. **The fake did not fail. It relocated the rule to the one place it cannot be enforced.**

---

## Step 8 — `xmin`, proved with two contexts

Episode 16's second debt, and the one nothing else in the toolchain can see at all.

```csharp
    [Fact]
    public async Task The_second_of_two_people_writing_to_the_same_copy_is_refused()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good);
        copy.Reserve();
        copy.Collect();
        copy.Return();
        copy.BeginInspection();

        await using (CatalogDbContext seeding = Database.NewDbContext())
        {
            seeding.Copies.Add(copy);
            await seeding.SaveChangesAsync();
        }

        // Two staff members, two screens, one box on the bench between them.
        await using CatalogDbContext inspector = Database.NewDbContext();
        await using CatalogDbContext colleague = Database.NewDbContext();

        Copy asInspectorSeesIt = await inspector.Copies.SingleAsync();
        Copy asColleagueSeesIt = await colleague.Copies.SingleAsync();

        asInspectorSeesIt.SendForRepair();
        await inspector.SaveChangesAsync();

        asColleagueSeesIt.Shelve();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => colleague.SaveChangesAsync());
    }
```

### Two contexts, and it has to be two

A `DbContext` tracks one instance per primary key. Query the same copy twice in one context and you
are handed the same object both times — so a single-context version of this test would have one
`Copy` in memory, both calls would apply to it, and `SaveChangesAsync` would write the result of
both. That is not a smaller version of the bug. **It is the bug not happening**, because the two
users were never two users.

Two contexts is the smallest honest model of two people: two connections, two loads, two `xmin`
values captured at load time, two `UPDATE`s.

### What the test is asserting, in shop terms

`SendForRepair()` and `Shelve()` are both legal moves out of `InInspection` — episode 15 built them
that way. So this is not an illegal transition being refused by the domain; both writes are perfectly
valid on their own. **The domain cannot help here at all.** Episode 15 said so in its own words —
*every guard in this class protects one object in-process* — and this is the sentence being cashed.

Without the token, both `UPDATE`s succeed. The last one wins. The box is on the shelf, the repair is
not recorded anywhere, and nothing anywhere logged a problem. The discovery is a customer opening the
box.

With it, EF puts the loaded `xmin` in the `WHERE` clause, matches zero rows, and raises
`DbUpdateConcurrencyException` — which the colleague's screen turns into "somebody changed this,
reload". Episode 20 maps it to a `409`; episode 23 is where a real write path raises it.

### The red, by removal, and it is invisible everywhere else

Delete the three lines from `CopyConfiguration`:

```csharp
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
```

`dotnet build` — clean. Then try to repeat step 7's four commands and notice that **there is nothing
to repeat**:

```bash
dotnet ef migrations add DropTheConcurrencyToken --project src/Catalog/BrickShare.Catalog.Api
```

The migration is generated with an **empty `Up` and an empty `Down`**, because `xmin` was never a
column of ours; it belongs to Postgres, the provider never emitted an operation for it, and removing
the property therefore has nothing to un-emit. There is no diff to review, nothing in the migration
file, nothing in `\d+ copies`. **Compared with step 7, there is not even a generated file to catch it
in** — and the rollback is correspondingly trivial:

```bash
dotnet ef migrations remove --project src/Catalog/BrickShare.Catalog.Api
```

then paste the three lines back and `dotnet test`. No `database update`, no snapshot to reason about,
nothing to undo, because nothing about the schema ever changed.

An empty migration is worth thirty seconds on its own, because students read one as a tool failure.
It is not: it is the generator correctly reporting that the model changed in a way the database does
not care about. Delete it and move on.

One test in the repository goes red. That is the entire safety net for this line, which is a fair
argument for the test existing and a fairer one for writing the deletion down in the script so that
whoever reads this file in a year knows what the three lines are for.

### The limits, restated because this is where people stop listening

- It protects writes made **through this `DbContext`**. `psql`, a migration script and a support tool
  all bypass it.
- It is *optimistic*: it detects a collision, it does not prevent one. Something has to catch the
  exception or the collision merely becomes a `500`, which is episode 20's job.
- It says nothing about two writes that are *both* fine. It is not a lock.

---

## Step 9 — `/health/ready`, against a database that exists

One more file, and it is the only test in the suite that runs `Program.cs`.

`tests/BrickShare.Catalog.IntegrationTests/CatalogApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BrickShare.Catalog.IntegrationTests;

/// <summary>
/// The API, wired to the test container. It overrides one configuration key and nothing else:
/// every service registration in Program.cs is the one that runs in production.
/// </summary>
public sealed class CatalogApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = connectionString
            }));
}
```

on the fixture:

```csharp
    public CatalogApiFactory Api { get; private set; } = null!;
```

```csharp
        // ...after MigrateAsync and the Respawner, in InitializeAsync:
        Api = new CatalogApiFactory(ConnectionString);
```

```csharp
    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await _postgres.DisposeAsync();
    }
```

and the test, `tests/BrickShare.Catalog.IntegrationTests/HealthChecks/ReadinessTests.cs`:

```csharp
using System.Net;

namespace BrickShare.Catalog.IntegrationTests.HealthChecks;

public class ReadinessTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task Ready_reports_healthy_when_the_database_answers()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### Override the input, not the object graph

This is the part worth the step, because the pattern almost everybody writes first is this:

```csharp
// The version in most tutorials, and it is testing the wrong thing.
protected override void ConfigureWebHost(IWebHostBuilder builder) =>
    builder.ConfigureServices(services =>
    {
        services.RemoveAll<DbContextOptions<CatalogDbContext>>();
        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));
    });
```

It works. And it has **deleted the registration from `Program.cs` and put a different one in its
place** — so whatever the test then proves, it proves about the test's registration. Any mistake in
the real one, any option set alongside it, any interceptor or logging filter added in episode 29, is
now outside the test's reach and the test stays green regardless.

The version above changes **one configuration key**. `Program.cs` reads
`builder.Configuration.GetConnectionString("Catalog")`, so supplying that key is the entire override,
and every registration under test is the one that ships. *Change the input, not the graph* — and the
reason the input is so small is that episode 16 read the connection string from configuration instead
of hard-coding it, which at the time looked like ordinary good manners.

### Provider ordering, for the third time

`ConfigureAppConfiguration` appends its source after the application's own, so it wins over
`appsettings.json` and `appsettings.Development.json`. That is the same mechanism episode 16 step 2
spent a paragraph on:

- `appsettings.Development.json` supplies `localhost` for `dotnet run`.
- `ConnectionStrings__Catalog` in Compose supplies `postgres` for the container.
- An in-memory source supplies a container port that did not exist thirty seconds ago, for tests.
- App Service supplies a real one in episode 18.

**Four consumers, one key, and the image never learns which one it is running under.** That is what
configuration providers are for, and this is the fourth angle on it — at which point it has stopped
being a framework feature and started being the reason the same artifact can be tested and deployed.

### Fourteen episodes of an empty predicate

Episode 3 built two health endpoints with nothing to check and argued the difference between them
anyway. Episode 16 registered the database check and then watched `/health/ready` be honestly
unhealthy in Azure, because there is no Postgres there yet.

**This is the first time it is green for a reason**, and the assertion is one line. `AddDbContextCheck`
calls `CanConnectAsync`, so a 200 here means: the container is up, the connection string reached it,
the DI graph in `Program.cs` composed, and the health check found the context it was told about.
Episode 4's argument for writing an integration test before any unit test — *at this point the only
thing that can break is composition* — is still the argument, and this is the first version of it
with a dependency in the picture.

### A scope note, said plainly

This step goes a little beyond "collect episode 16's two debts", and it is a judgement call rather
than an obvious inclusion. Two reasons it is in: without it, `CatalogApiFactory` has no consumer and
the pattern arrives cold in episode 20 in the middle of a different lesson; and it is the only test
in the whole repository that runs `Program.cs` against anything real. One test, and it buys the
harness that episodes 20, 22, 23, 24 and 25 all sit on.

---

## Step 10 — The pipeline, and the episode where nothing changes

Open `.github/workflows/ci.yml` and `deploy.yml` on camera and **change nothing**.

That is the payoff of episode 5, and it is worth naming because it is the sort of thing that only
looks free in hindsight: `ubuntu-latest` runners have a Docker daemon, `dotnet test` starts its own
container, and the workflow's `dotnet test --no-build --configuration Release` line does not know any
of this happened.

### The costs, honestly

- **`dotnet test` now requires Docker.** That is a real change to what a fresh clone needs, and it is
  the biggest single cost of this episode. It has been true since episode 5 for `docker compose up`,
  so nobody in this course is newly blocked — but a student who was running the tests on a machine
  with Docker installed-and-never-started will find out today.
- **An image pull and a few seconds of start-up per run**, once for the whole suite rather than per
  test. Locally the layer is cached after the first time; on a fresh CI runner it is not, and
  `postgres:18` is not a small image.
- **Ryuk.** Testcontainers starts a second, tiny container — the resource reaper — whose job is to
  remove your containers if the test process dies without cleaning up. It is why a killed debugger
  does not leave five Postgres containers behind. Some corporate Docker setups block it, and the
  escape hatch is `TESTCONTAINERS_RYUK_DISABLED=true`, at the price of exactly the cleanup it was
  doing. Mention it because the students who need it will not guess it.

### The local speed-up that is not committed

```csharp
    // Tempting. Not taken.
    .WithReuse(true)
```

Container reuse keeps the container alive between test runs, and the second run onwards starts
instantly. It also keeps the *data*, which is precisely the isolation step 5 just bought — and it
survives the reset in a way that is easy to reason about right up until the schema changes and the
reused container still has the old one.

**It is a legitimate local option and a bad default**, which is why it belongs in a sentence here
rather than in the file: someone who chooses it has understood the trade, and someone who inherits it
has not.

---

## Step 11 — Through the gates

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

```bash
docker ps   # in another terminal, during the run
```

PR, green, merge, deployed.

**Azure is unchanged today** — there is still no Postgres in Terraform until episode 18, so the
deployed app still reports `/health/ready` unhealthy, exactly as episode 16 left it and for exactly
the same correct reason. Today's work happened entirely inside `dotnet test`.

---

---

## The fixture in one piece

Steps 3, 5 and 9 build `CatalogDatabase` a third at a time, which is the right order to *explain* it
and an annoying order to *type* it. Here it is assembled, as the file ends up in the repository.

`tests/BrickShare.Catalog.IntegrationTests/CatalogDatabase.cs`:

```csharp
using BrickShare.Catalog.Api.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Respawn;

using Testcontainers.PostgreSql;

namespace BrickShare.Catalog.IntegrationTests;

/// <summary>
/// The Postgres every integration test runs against: one container for the whole test run,
/// created by the same migration that will run in the pipeline, and reset before each test.
/// </summary>
public sealed class CatalogDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("brickshare_catalog")
        .WithUsername("brickshare")
        .WithPassword("brickshare")
        .Build();

    private Respawner _respawner = null!;

    public CatalogApiFactory Api { get; private set; } = null!;
    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Yes, this is Database.Migrate() at startup. See step 3: the prohibition is about
        // several application instances racing, and there is exactly one writer here.
        await using CatalogDbContext context = NewDbContext();
        await context.Database.MigrateAsync();

        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        // After MigrateAsync, because Respawn reads the foreign-key graph to work out a
        // safe delete order, and an empty database has no graph to read.
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],

            // Clear this and the database forgets it has a schema, so the next `dotnet ef`
            // command tries to apply InitialCatalog to a database that already has the table.
            TablesToIgnore = ["__EFMigrationsHistory"]
        });

        Api = new CatalogApiFactory(ConnectionString);
    }

    /// <summary>
    /// Empties every table, in an order the foreign keys allow. Called before each test.
    /// </summary>
    public async Task ResetAsync()
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);
    }

    /// <summary>
    /// A new context on the same database. Tests that need to see a row the way another
    /// connection would ask for two of these rather than clearing a change tracker.
    /// </summary>
    public CatalogDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
```

**One note for anyone typing along rather than recording:** `IAsyncLifetime` here is xUnit **v2**'s,
so both methods return `Task`. On xUnit v3 they return `ValueTask`, and the compiler error is
unhelpful enough to be worth knowing in advance. This repository is on `xunit` 2.9.3.

And the three smaller files, for completeness — `DatabaseCollection.cs`, `DatabaseTest.cs` and
`CatalogApiFactory.cs` — each appear whole in steps 4 and 9 and need no assembly.

## What this episode deliberately does not do

- **No test that `status` and `grade` are stored as names.** This is the omission to state plainly
  rather than bury, because episode 16 called the enum-ordinal failure its most frightening moment
  and it stays where episode 16 left it: a manual step in that episode's verification list, run by
  hand in `psql`, protected by a reviewer reading a generated migration file. The automated version
  is short, and anyone who wants it can add it:

  ```csharp
  // Asserted by a client that has never heard of the mapping.
  await using NpgsqlConnection connection = new(Database.ConnectionString);
  await connection.OpenAsync();

  await using NpgsqlCommand command = new("SELECT status, grade FROM copies", connection);
  await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

  Assert.True(await reader.ReadAsync());
  Assert.Equal("Available", reader.GetString(0));
  Assert.Equal("New", reader.GetString(1));
  ```

  The line drawn for this episode is *claims that need a real engine and have no other check*. The
  enum has another check — the reviewable migration file — and the unique index and `xmin` have
  none. That is a defensible line and it is not a free one, and saying which is which is the point.
- **No `timestamptz` test.** Npgsql refuses a `DateTimeOffset` with a non-zero offset, which is a
  genuinely useful behaviour to see; it is also a driver rule rather than a rule of this system, and
  the assertion available is on an exception message. Episode 16's verification covers it by hand.
- **No assertions against `information_schema`.** A test that reads back `character varying(32)` is
  a transcription of the migration, which is a transcription of `CopyConfiguration`. Three copies of
  one sentence, and none of them is a rule about LEGO boxes.
- **No `numeric(10,2)` test** — there is still no money column. `MoneyConverter` and the convention
  in `ConfigureConventions` get their first real column in episode 22.
- **No HTTP tests beyond readiness**, because there are no endpoints. Episode 20 brings the first
  one, and it arrives into a harness that already exists.
- **No repository, no unit of work.** Episode 16 deferred the question with a reason — *episode 17's
  tests run against a real Postgres, so the usual reason for the abstraction, faking the database, is
  one this course deliberately does not want* — and today that reason is discharged rather than
  deferred again.
- **No coverage reporting.** Still episode 11's open question, and there is now a body of tests worth
  describing, which is what episode 11 said the trigger would be. It is not decided yet which episode
  takes it.
- **No Postgres in Azure** (episode 18) and **no migration in the pipeline** (episode 19).

## Verification

```bash
dotnet format --verify-no-changes  # exits 0
dotnet build                       # 0 warnings, 0 errors
dotnet test                        # green: 69 unit, 1 liveness, 4 new — the suite takes ~3s
```

```bash
# In a second terminal while the suite runs: a postgres:18 and a ryuk appear, then both go.
watch -n 0.5 docker ps
```

Then four deliberate breakages, and the interesting column is **what else noticed**. Rows two and
three need a migration generated and then removed again — the commands are in steps 7 and 8, and
`git status` clean afterwards is the check that the rollback worked:

| Break | What fails | What else noticed |
| --- | --- | --- |
| Stop Docker, `dotnet test` | Every test in the collection, with a Testcontainers message naming the daemon | Nothing — and this is the cost of the choice, visible in ten seconds |
| Delete `.IsUnique()`, then `migrations add DropLabelUniqueness` | `Two_copies_cannot_carry_the_same_label_code` | The migration diff — a `unique: true` that is *not there*, if somebody reads it |
| Delete the three `xmin` lines, then `migrations add` anything | `The_second_of_two_people_writing_to_the_same_copy_is_refused` | **Nothing at all.** No build error, an empty migration, no schema change |
| Point `CatalogApiFactory` at a wrong port | `Ready_reports_healthy_when_the_database_answers`, with a 503 | Nothing |

Row three is the one to sit with. Before this episode, deleting those three lines was a change that
**no tool in this repository could detect** — not the compiler, not the analyzers, not `dotnet ef`,
not a reviewer looking at a diff, because there is no diff outside the file itself. That is the
clearest possible answer to "what is an integration test for", and it took a real database to give
it.

And one last check that the isolation works, because it is the thing that fails silently:

```bash
dotnet test --filter "FullyQualifiedName~Persistence"   # green
dotnet test                                            # green, same tests, more neighbours
```

A suite that is green alone and green together is the whole claim step 5 makes. If reset-before ever
regresses, this is the pair of commands that disagrees.

## Next

[Episode 18 — Terraform: Postgres and managed identity](episode-18.md):
the database this episode proved the mapping against still does not exist in Azure.

Episode 16 deployed a `DbContext` with nothing to connect to, and it has been sitting there reporting
`/health/ready` unhealthy ever since — correctly, and with nothing acting on the answer because
`health_check_path` is not set. Episode 18 creates the server, and does it **without a password
anywhere**: the App Service connects as its own managed identity, which is the same argument episode
8 made about the container registry, arriving somewhere with much more to lose.

And when it does, `health_check_path` gets set, and the three episodes of health-check groundwork —
3, 16, 17 — finally have consequences in production.
