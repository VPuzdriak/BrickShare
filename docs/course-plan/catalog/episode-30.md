# Episode 30 — All of them or none of them

← [Course plan](catalog-api.md) · Previous: [Episode 29 — A copy belongs to a set](episode-29.md)

Episode 29 ended by naming its own defect out loud:

> **No batch.** Four boxes is four calls, and every one of them is a separate transaction — so a
> till-side interruption after the second leaves two copies registered and two not.

That is the episode. The shop that bought four Titanics does not want four requests, and it very
much does not want two of them to work. This one turns `POST /catalog/sets/{setId}/copies` into an
endpoint that registers a list of **any** length — one, four, seventeen — in one transaction, and
then lets the validation layer grow up to match, because a batch is the first request in this
service where "which field is wrong" needs an index on the end of it.

**Done when** one request registers seventeen copies, a request with one bad weight in it registers
**none** of them, and no handler in the service calls a validator by hand.

> **Runtime: about 17 minutes — over budget, and knowingly.** The clean split is marked in place,
> between step 5 and step 6: **30a** is the batch and the transaction (steps 1–5, ~11 minutes),
> **30b** is the validation refactor (steps 6–9, ~7 minutes). Record it as one if the pace holds;
> cut at the marker if it does not. If it has to stay one episode and still run short, **step 5 is
> the cut** — the collision test is the most skippable thing here, and it is the only part that is
> about a one-in-729-million event.

## Before recording

- Episode 29 merged: `POST /catalog/sets/{setId}/copies` registers one copy, and the four tests in
  `RegisterCopyTests.cs` are green.
- `docker compose up` for Postgres, `dotnet user-secrets` still holding your Rebrickable key.
- A branch. **No migration this episode** — the schema does not change, only the shape of a request.
- `episode-29.md` open at *"No `ILabelCodeMinter`, still"*. Step 4 reverses that decision on camera,
  and the reversal reads much better with the sentence that allowed it visible:
  *"Episode 30 is allowed to change its mind if the batch makes it necessary."*

**Two of the nine steps are not driven by a test, and both say so where they appear**: the package
reference in step 7 is configuration, and step 9 is a request collection. Everything else goes red
first.

Every sample below names its file and where in it the code goes. Where something is edited in a file
that already exists, the **first block is what is already there** — the anchor to find on screen —
and the **second block is what to paste**. Every block is copy-paste clean: no markers, no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/IntegrationTests/Copies/RegisterCopiesTests.cs` | **New** — replaces `RegisterCopyTests.cs`, five tests |
| `src/…/Api/Endpoints/CopyEndpoints.cs` | The request becomes a list; `AddRange`; one save |
| `src/…/Domain/ILabelCodeMinter.cs` | **New** — the seam episode 29 refused |
| `src/…/Domain/LabelCode.cs` | `Prefix` and `MaxLength` come out of hiding |
| `src/…/Api/Persistence/CopyConfiguration.cs` | `HasMaxLength(10)` → `HasMaxLength(LabelCode.MaxLength)` |
| `tests/…/IntegrationTests/Copies/ScriptedLabelCodeMinter.cs` | **New** — six lines, one job |
| `src/…/Api/Endpoints/RegisterCopiesRequestValidator.cs` | **New** — replaces `RegisterCopyRequestValidator.cs` |
| `src/…/Api/Endpoints/ValidationFilter.cs` | **New** |
| `src/…/Api/Endpoints/CatalogSetEndpoints.cs`, `CatalogLookupEndpoints.cs` | The validator call comes out of both |
| `src/…/Api/Program.cs` | Three registrations become one; the minter |
| `Directory.Packages.props`, `BrickShare.Catalog.Api.csproj` | `FluentValidation.DependencyInjectionExtensions` |
| `src/…/Api/BrickShare.Catalog.Api.http` | Requests 7–11 replaced |

---

## Step 1 — Two boxes on the shelf, and no rows

No code in this step. Start `docker compose up`, run episode 29's requests 7 and 8, then **kill the
API** and run 9 and 10 against nothing.

```sql
select label_code, baseline_weight_grams from copies;
```

Two rows. The delivery was four boxes.

**Caveman version:** shop buy four box. Shop call four time. Third call die. Two box have sticker,
two box have nothing, computer say shop own two Titanic. Computer wrong. Box still on shelf.

That is the whole problem, and it is worth being precise about *why* it is a problem, because "four
calls is chatty" is not the reason. Chatty is a performance complaint. This is a **correctness**
complaint: after the failure, the shelf and the database disagree, and **nothing in the system knows
they disagree.** Nobody gets an error. The staff member sees three `201`s and one dead connection
and has no way to know whether the fourth box got a row, so the safe thing to do is retry — and the
retry either works or registers a fifth Titanic that does not exist.

### Why this is a refactor and not a new endpoint

The obvious move is to add `POST /catalog/sets/{setId}/copies/batch` next to the existing endpoint
and leave the one-copy version alone. Do not.

| | Refactor the endpoint (chosen) | Add `/copies/batch` beside it |
| --- | --- | --- |
| The broken path | Gone. It cannot be called because it does not exist | Still there, still shipped, still the shorter URL |
| One copy | A list of one. Four extra characters on the wire | Two endpoints that do the same thing |
| The next reader | One way to register a copy | Two, and no way to tell which is intended |

**A batch is not a second feature, it is the correct version of the first one.** Registering one
copy is registering a delivery that happened to contain one box. The single-copy endpoint was never
a different operation — it was this operation with the collection flattened out of it, which is
exactly the mistake episode 29 said it was making on purpose.

The cost is real and worth saying: this breaks every existing client. There are no existing clients,
because episode 38 has not added authentication and there is nothing in front of this service yet.
**That is the cheapest moment an API shape will ever be wrong, and it is now.**

---

## Step 2 — Red: the request grows a list

Delete `RegisterCopyTests.cs` and write the batch version. This is a build error against a shape
that does not exist yet, which `CLAUDE.md` counts as a legitimate red.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RegisterCopiesTests.cs — new file, replaces RegisterCopyTests.cs
using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Copies;

public class RegisterCopiesTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task One_box_is_a_delivery_of_one()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { copies = new[] { Weighing(9200) } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);

        CopyResponse copy = Assert.Single(registered.Copies);

        Assert.Equal(setId, copy.CatalogSetId);
        Assert.Equal(ConditionGrade.New, copy.Grade);
        Assert.Equal(CopyStatus.Available, copy.Status);
        Assert.Equal(9200, copy.BaselineWeightInGrams);

        // The shape, not the value. The value is the server's business.
        Assert.Matches("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$", copy.LabelCode);
    }

    [Fact]
    public async Task Seventeen_boxes_arrive_in_one_call_and_get_seventeen_labels()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        // Seventeen different weights, because seventeen boxes are seventeen objects.
        object[] delivery = [.. Enumerable.Range(0, 17).Select(index => Weighing(9200 + index))];

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { copies = delivery });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);
        Assert.Equal(17, registered.Copies.Count);
        Assert.Equal(17, registered.Copies.Select(copy => copy.LabelCode).Distinct().Count());

        // The order matters: the third label in the response belongs to the third box in the
        // delivery, and that is how staff know which sticker goes on which box.
        Assert.Equal(
            Enumerable.Range(0, 17).Select(index => 9200 + index),
            registered.Copies.Select(copy => copy.BaselineWeightInGrams));

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(17, await dbContext.Copies.CountAsync(copy => copy.CatalogSetId == setId));
    }

    [Fact]
    public async Task One_bad_weight_registers_none_of_them()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { Weighing(9200), Weighing(9187), Weighing(0), Weighing(9210) } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);

        // The index is the point. "One of your four weights is wrong" is not a usable answer.
        Assert.Contains("copies[2].baselineWeightInGrams", problem.Errors.Keys);

        // And the reason this test exists at all.
        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(0, await dbContext.Copies.CountAsync());
    }

    [Fact]
    public async Task An_empty_delivery_is_not_a_delivery()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies", new { copies = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("copies", problem.Errors.Keys);
    }

    [Fact]
    public async Task A_copy_of_a_set_nobody_catalogued_is_not_a_copy_of_anything()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{Guid.CreateVersion7()}/copies",
            new { copies = new[] { Weighing(9200) } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("catalogue", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static object Weighing(int grams) =>
        new { grade = "New", baselineWeightInGrams = grams };
}
```

**Caveman version:** test say — send seventeen box, get seventeen sticker. Test say — send one bad
box, get zero sticker and zero row. Zero. Not three.

Two things to point at before anything compiles.

**`Assert.Equal(0, await dbContext.Copies.CountAsync())` is the episode.** Every other assertion here
is about a response body. That one is about what is *not* in the database, and it is the only
assertion in the file that would still have failed if the endpoint were written the obvious way —
loop, validate, insert, keep going.

**The request is an object, not a bare array.** `{ "copies": [ … ] }` rather than `[ … ]`. A bare
array is shorter and is the wrong shape for two reasons: a JSON array has no room to grow a field
later — the day someone wants `"deliveryNote"` on the request there is nowhere to put it without
breaking every client — and an array root has no name, so FluentValidation's error keys come back
hanging off nothing. `copies[2].baselineWeightInGrams` tells a client exactly which box to reweigh.
`[2].baselineWeightInGrams` makes it guess what the root was.

---

## Step 3 — Green: one `AddRange`, one save

The request and response types first. `RegisterCopyRequest` becomes two records — the envelope and
the item — because the thing being validated and the thing being registered are no longer the same
shape.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — as it is, at the bottom of the file
public sealed record RegisterCopyRequest(ConditionGrade Grade, int BaselineWeightInGrams);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — replace it with these three
/// <summary>
/// One delivery. An envelope rather than a bare array, so the request can grow a field without
/// breaking every client, and so validation errors have a root to name.
/// </summary>
public sealed record RegisterCopiesRequest(IReadOnlyList<CopyToRegister> Copies);

/// <summary>
/// One box in the delivery. Everything a copy needs that the shop has to look at the box to know.
/// </summary>
public sealed record CopyToRegister(ConditionGrade Grade, int BaselineWeightInGrams);

public sealed record RegisterCopiesResponse(IReadOnlyList<CopyResponse> Copies);
```

Now the handler. Replace everything from `RegisterAsync` down to `RegisterWithAMintedLabelAsync`'s
closing brace:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — replace RegisterAsync and RegisterWithAMintedLabelAsync
    private static async Task<Results<Created<RegisterCopiesResponse>, ValidationProblem, ProblemHttpResult>>
        RegisterAsync(
            Guid setId,
            RegisterCopiesRequest request,
            IValidator<RegisterCopiesRequest> validator,
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
            return TypedResults.Problem(
                title: "No such set",
                detail: $"Set {setId} is not catalogued. Catalogue it first at /api/v1/catalog/sets.",
                statusCode: StatusCodes.Status404NotFound);
        }

        IReadOnlyList<Copy> copies =
            await RegisterWithMintedLabelsAsync(setId, request, database, cancellationToken);

        // Seventeen copies have no single location. This points at the collection they are now in —
        // a GET that arrives in episode 34. Episodes 28 and 29 made the same deferral for the same
        // reason: inventing an endpoint to make a header true is building a feature to satisfy a
        // string.
        return TypedResults.Created(
            $"/api/v1/catalog/sets/{setId}/copies",
            new RegisterCopiesResponse([.. copies.Select(CopyResponse.From)]));
    }

    private static async Task<IReadOnlyList<Copy>> RegisterWithMintedLabelsAsync(
        Guid setId,
        RegisterCopiesRequest request,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MintAttempts; attempt++)
        {
            List<Copy> copies =
            [
                .. request.Copies.Select(copy =>
                    Copy.Register(setId, LabelCode.Mint(), copy.Grade, copy.BaselineWeightInGrams))
            ];

            database.Copies.AddRange(copies);

            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return copies;
            }
            catch (DbUpdateException ex) when (IsLabelAlreadyTaken(ex) && attempt < MintAttempts)
            {
                // Step 4 has something to say about this line.
                foreach (Copy copy in copies)
                {
                    database.Entry(copy).State = EntityState.Detached;
                }
            }
        }

        // The last attempt either returns or lets its exception out, because the filter above stops
        // catching once attempt reaches MintAttempts. The compiler cannot see that: definite-return
        // analysis does not reason about exception filters, so it needs to be told.
        throw new UnreachableException();
    }
```

Green. Four of the five tests pass; the error-key assertion in `One_bad_weight_registers_none_of_them`
is still red until step 6 teaches the validator about the list, and that is fine — it is the
validation half of the episode and it has its own steps.

**Caveman version:** make all copy. Put all copy in box. Push box once. Database say yes to all or
no to all. No middle.

### Where is the transaction?

There isn't one, in the sense of a line of code, and that is the point worth spending a minute on
because it is the single most common thing to over-build here.

```csharp
await using IDbContextTransaction transaction = await database.Database.BeginTransactionAsync(cancellationToken);
// …
await transaction.CommitAsync(cancellationToken);
```

That would be ceremony. **`SaveChangesAsync` already opens a transaction** around everything it
writes, unless one is already open — one `BEGIN`, seventeen inserts, one `COMMIT`. Turn on EF's SQL
logging and show it, because a student who has not seen the log believes the transaction is the
thing you type.

An explicit transaction earns its place when a unit of work spans **more than one**
`SaveChangesAsync`, or has to read at a particular isolation level, or has to enclose something that
is not EF. None of those is true here. **Seventeen inserts is one `SaveChanges`, and one
`SaveChanges` is already all-or-nothing.**

The corollary is the rule to actually carry away: *the atomic unit is the save, so the code's job is
to have exactly one of them.* The broken version of this endpoint is not the one missing a
`BeginTransaction` — it is the one with a `SaveChangesAsync` inside a `foreach`.

### One `AnyAsync`, not seventeen

The set is checked **once**, outside the loop, before anything is minted. It is the same set for
every box in the delivery — it is in the path, not in the body — so checking it per item would be
seventeen round trips for one answer. This is the shape of most batch bugs: **what varies per item
and what is constant for the request are different things, and code written from the single-item
version tends to put both inside the loop.**

---

## Step 4 — The recovery that would have eaten a box

Look again at the block step 3 left a comment on.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — as episode 29 wrote it
            catch (DbUpdateException ex) when (IsLabelAlreadyTaken(ex) && attempt < MintAttempts)
            {
                // A failed SaveChanges leaves the entity Added. Without this the retry writes two
                // rows, one of which still carries the label that just collided.
                database.Entry(copy).State = EntityState.Detached;
            }
```

Episode 29 detached **the one copy**, because there was only ever one. Step 3's version detaches all
of them and re-mints all of them, and that difference is not a detail.

Suppose seventeen boxes are minted and label number nine collides with a row that already exists.
Postgres reports **one** violation — the first one it hits — and EF hands back a `DbUpdateException`
that names a constraint, not a row. So:

| Recovery | What happens |
| --- | --- |
| Detach the colliding copy, retry the rest | You do not know which copy collided. `ConstraintName` is `ix_copies_label_code`, not "row 9" |
| Detach nothing, retry | Seventeen entities are still `Added` with their old labels. The retry adds seventeen more. Thirty-four inserts, and the same collision |
| **Detach all, re-mint all, retry** | Seventeen new labels, one save, and the only cost is seventeen `Guid`s nobody will ever see |

The third is the only one that is correct without information the exception does not carry. It
re-mints sixteen labels that were fine, which sounds wasteful and is not: nothing was written, the
`Guid`s were free, and **a correct retry that does slightly too much work beats a clever one that
needs a fact the database did not give it.**

### The interface episode 29 refused

Here is the honest part, and it should be said as a reversal rather than slipped in.

Episode 29 argued **against** `ILabelCodeMinter`, and the argument was good: the retry branch was
three lines that read correctly, introducing an interface with one implementation to test three
readable lines is the kind of indirection this course keeps refusing, and the branch had no logic in
it worth protecting.

**That stopped being true one step ago.** The branch now contains a decision — *which copies get
re-minted* — and the answer is non-obvious enough that the table above needed three rows to explain
it. **A branch with no logic in it does not need a test. A branch with a decision in it does**, and
the only way to reach this one from a test is to make minting something a test can steer.

And while we are here, correct something the course plan promised:

> the mint collision episode 29 could not reach from a test becomes reachable, because a batch mints
> several labels inside one transaction

**That is not true, and the arithmetic is worth doing on camera.** The alphabet has 30 characters
and the code is 6 of them: 30⁶ = 729,000,000 labels. A batch raises the chance of a collision
*within itself* by the birthday rule, which means a request would need somewhere around 40,000
copies before a self-collision was even likely. A batch makes the branch **more probable**; it does
not make it **reachable**. The seam is what makes it reachable, and the batch is what makes the seam
worth buying. Those are different claims and the plan ran them together.

```csharp
// src/Catalog/BrickShare.Catalog.Domain/ILabelCodeMinter.cs — new file
namespace BrickShare.Catalog.Domain;

/// <summary>
/// Issues label codes. One implementation ships. The interface exists because the code that
/// recovers from a collision has a decision in it, and a decision that cannot be reached from a
/// test is a decision nobody has checked.
/// </summary>
public interface ILabelCodeMinter
{
    LabelCode Next();
}

/// <summary>
/// The one that ships. Every label BrickShare has ever printed came from here.
/// </summary>
public sealed class RandomLabelCodeMinter : ILabelCodeMinter
{
    public LabelCode Next() => LabelCode.Mint();
}
```

Both types in one file on purpose: an interface and its only implementation, nine lines together,
and splitting them into two files would mean opening two files to read one idea.

Inject it, and stop calling the static:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — RegisterAsync's parameter list gains one line
            Guid setId,
            RegisterCopiesRequest request,
            IValidator<RegisterCopiesRequest> validator,
            ILabelCodeMinter minter,
            CatalogDbContext database,
            CancellationToken cancellationToken)
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the call, and the helper's signature and body
        IReadOnlyList<Copy> copies =
            await RegisterWithMintedLabelsAsync(setId, request, minter, database, cancellationToken);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — replace the helper's signature and its first statement
    private static async Task<IReadOnlyList<Copy>> RegisterWithMintedLabelsAsync(
        Guid setId,
        RegisterCopiesRequest request,
        ILabelCodeMinter minter,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MintAttempts; attempt++)
        {
            List<Copy> copies =
            [
                .. request.Copies.Select(copy =>
                    Copy.Register(setId, minter.Next(), copy.Grade, copy.BaselineWeightInGrams))
            ];
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — with the other singletons, below AddSingleton(TimeProvider.System)
// Singleton: it holds nothing. RandomNumberGenerator.GetString is thread-safe and the minter has
// no state of its own to share badly.
builder.Services.AddSingleton<ILabelCodeMinter, RandomLabelCodeMinter>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — with the other usings
using BrickShare.Catalog.Domain;
```

---

## Step 5 — Red, then green: the collision, finally under test

*(If the episode is running long, this is the step to cut. It is the only part of the episode that
is about an event with eight zeroes in the denominator.)*

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/ScriptedLabelCodeMinter.cs — new file
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.IntegrationTests.Copies;

/// <summary>
/// Hands out the labels it was given, in order, and repeats the last one once it runs out.
/// The whole point of the seam: a mint whose next answer the test already knows.
/// </summary>
internal sealed class ScriptedLabelCodeMinter(params string[] labels) : ILabelCodeMinter
{
    private int _issued;

    public LabelCode Next()
    {
        string label = labels[Math.Min(_issued, labels.Length - 1)];
        _issued++;

        return LabelCode.Parse(label);
    }
}
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RegisterCopiesTests.cs — a sixth test, above the Weighing helper
    [Fact]
    public async Task A_batch_that_mints_the_same_label_twice_mints_the_whole_batch_again()
    {
        // Attempt one collides with itself; attempt two is two labels nobody holds.
        ScriptedLabelCodeMinter minter = new(
            "BRK-AAAAAA", "BRK-AAAAAA",
            "BRK-BBBBBB", "BRK-CCCCCC");

        using WebApplicationFactory<Program> api = Database.Api.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<ILabelCodeMinter>(minter)));

        HttpClient client = api.CreateClient();
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { Weighing(9200), Weighing(9187) } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);

        // Both labels are from the second attempt. Not one kept and one re-minted — a box was
        // never going to be dropped, and this is the assertion that says so.
        Assert.Equal(["BRK-BBBBBB", "BRK-CCCCCC"], registered.Copies.Select(copy => copy.LabelCode));

        await using CatalogDbContext dbContext = Database.NewDbContext();
        Assert.Equal(2, await dbContext.Copies.CountAsync());
    }
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RegisterCopiesTests.cs — with the other usings
using BrickShare.Catalog.Api;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
```

**Caveman version:** fake sticker machine. Machine give same sticker two time on purpose. Database
say no. Code throw away both sticker, ask machine again, get two new sticker. Two box, two row,
nobody lose box.

Three things in that test are worth a sentence each.

**`WithWebHostBuilder` rather than a change to `CatalogApiFactory`.** `Database.Api` is shared by
every integration test in the run — one container, one host. Reaching into it to swap a service
would swap it for everybody. `WithWebHostBuilder` returns a **derived** factory: same configuration,
same container, one registration different, disposed at the end of this test. **A test that needs a
different world builds a different world; it does not edit the one everyone else is using.**

**`ConfigureTestServices`, not `ConfigureServices`.** `ConfigureServices` runs *before*
`Program.cs`'s registrations, so `AddSingleton<ILabelCodeMinter, RandomLabelCodeMinter>()` would run
afterwards and win. `ConfigureTestServices` runs *after*, and last registration wins. Getting this
backwards produces a test that passes for a reason that has nothing to do with what it says.

**The collision is caught by Postgres, not by C#.** Both copies carry `BRK-AAAAAA` in the same
`INSERT` batch, and the thing that notices is `ix_copies_label_code`. There is no in-memory check of
"have I minted this already", and there should not be: the label has to be unique across every copy
BrickShare has ever registered, not just the ones in this request, and **the database is the only
participant that can see all of them** — which is word for word episode 24's argument about set
numbers, arriving at a different answer. There, the violation meant *you asked for something already
true* and became a `409`. Here it means *I was unlucky* and becomes nothing at all.

### While we are in `LabelCode`

The one loose end from episode 22's habit. `CopyConfiguration` has been carrying a hard-coded `10`
since episode 16:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CopyConfiguration.cs — as it is
            .HasMaxLength(10)
```

Ten is `BRK-` plus six, computed once by a human and written down. Episode 22 made `SetNumber.MaxLength`
public so an error message could not quote a stale number; the same treatment here stops a column
from being the wrong size the day the code length changes.

```csharp
// src/Catalog/BrickShare.Catalog.Domain/LabelCode.cs — replace the two consts at the top of the record
    public const string Prefix = "BRK-";
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    public const int Length = 6;

    // Not a const, and it cannot be: string.Length is not a compile-time constant expression, so
    // C# will not let "Prefix.Length + Length" be one. static readonly is the closest available
    // thing, and it is read by CopyConfiguration — the only place this number ever mattered, and
    // the only place it was ever going to be wrong.
    public static readonly int MaxLength = Prefix.Length + Length;
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/LabelCode.cs — as it is
    public static LabelCode Mint() =>
        new($"BRK-{RandomNumberGenerator.GetString(Alphabet, Length)}");
```

```csharp
// src/Catalog/BrickShare.Catalog.Domain/LabelCode.cs — replace it
    public static LabelCode Mint() =>
        new($"{Prefix}{RandomNumberGenerator.GetString(Alphabet, Length)}");
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CopyConfiguration.cs — replace HasMaxLength(10)
            .HasMaxLength(LabelCode.MaxLength)
```

**And the one that stays hard-coded, on purpose:**

```csharp
    [GeneratedRegex("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$")]
    private static partial Regex Pattern();
```

`GeneratedRegex` needs a compile-time constant, and a `const` string built from the other consts is
not one either. Leaving it literal is the honest option: **a source generator that needs a literal
gets a literal, and pretending otherwise with a hand-concatenated string would be harder to read and
no more correct.** Say it out loud rather than letting a sharp student notice the inconsistency and
assume it was missed.

Run the migrations check: `dotnet ef migrations has-pending-model-changes` says no. Ten is still ten.

> ───────────── **cut here for a 30a / 30b split** ─────────────
>
> Everything above is the batch. Everything below is the validation layer catching up with it.
> If this is two episodes, 30b opens by running `One_bad_weight_registers_none_of_them` and showing
> the one red assertion left standing.

---

## Step 6 — Red is already on the screen: `RuleForEach`

`One_bad_weight_registers_none_of_them` has been failing since step 3, on exactly one line:

```
Assert.Contains() Failure: Key not found in dictionary
Key:   copies[2].baselineWeightInGrams
Keys:  ["copies"]
```

The current validator validates a `RegisterCopyRequest` that no longer exists in that form. What the
client gets today is one error against the envelope, which tells a staff member with seventeen boxes
on a counter that *something* is wrong with the delivery.

Delete `RegisterCopyRequestValidator.cs`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/RegisterCopiesRequestValidator.cs — new file, replaces RegisterCopyRequestValidator.cs
using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class RegisterCopiesRequestValidator : AbstractValidator<RegisterCopiesRequest>
{
    /// <summary>
    /// A hundred boxes. This is not a claim about delivery vans — it is a bound on one request, so
    /// a client that means to send five cannot accidentally ask this service to mint, hold and
    /// insert a hundred thousand rows in a single transaction that also holds a lock the whole time.
    /// </summary>
    public const int MaximumBatchSize = 100;

    public RegisterCopiesRequestValidator()
    {
        // NotEmpty covers both null and zero-length, which is what makes it the right rule here:
        // { } and { "copies": [] } and { "copies": null } are the same mistake told three ways.
        RuleFor(request => request.Copies).NotEmpty()
            .WithMessage("Register at least one copy. An empty delivery is not a delivery.");

        RuleFor(request => request.Copies)
            .Must(copies => copies is null || copies.Count <= MaximumBatchSize)
            .WithMessage(
                $"Register at most {MaximumBatchSize} copies in one request. "
                + "Split a larger delivery across several.");

        // One rule per box, and the error key carries the index of the box it failed on.
        RuleForEach(request => request.Copies).SetValidator(new CopyToRegisterValidator());
    }
}

public sealed class CopyToRegisterValidator : AbstractValidator<CopyToRegister>
{
    /// <summary>
    /// Fifty kilograms. The heaviest boxed LEGO set weighs about fifteen, so this is not a claim
    /// about LEGO — it is a typo filter. A baseline of 92000 grams instead of 9200 makes every
    /// future return of that copy look catastrophically short.
    /// </summary>
    public const int MaximumBaselineWeightInGrams = 50_000;

    public CopyToRegisterValidator()
    {
        // The backstop for the numeric form. A grade sent as "Sparkly" never reaches this rule —
        // the JSON reader refuses it first — but a grade sent as 99 deserializes happily.
        RuleFor(copy => copy.Grade).IsInEnum()
            .WithMessage("A grade is one of New, Excellent, Good or Fair.");

        RuleFor(copy => copy.BaselineWeightInGrams)
            .InclusiveBetween(1, MaximumBaselineWeightInGrams)
            .WithMessage(
                $"A baseline weight is in grams, between 1 and {MaximumBaselineWeightInGrams}. "
                + "Weigh the box while it is known complete.");
    }
}
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — as it is
builder.Services.AddScoped<IValidator<RegisterCopyRequest>, RegisterCopyRequestValidator>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — replace it (step 7 deletes all three of these)
builder.Services.AddScoped<IValidator<RegisterCopiesRequest>, RegisterCopiesRequestValidator>();
```

Green — all six tests.

**Caveman version:** one rule for delivery, one rule for each box. Box number two bad? Error say
box number two. Staff know which box go back on scale.

### Two validators, and which rule goes in which

The split is the thing to teach, and it is the same question episode 22 asked about the domain,
asked one level down.

| Rule | Where | Why |
| --- | --- | --- |
| At least one copy | Envelope | It is about the collection. A `CopyToRegister` cannot see whether it has siblings |
| At most a hundred | Envelope | Same |
| Grade is a real grade | Item | It is a property of one box and means nothing at the collection level |
| Weight is plausible | Item | Same |

**A validator's scope is the type it validates, and rules that need to see more than that type
belong to whatever can see more.** The tell is the `Must` on the envelope: `copies.Count` is a fact
about the list, not about any box in it.

### The error key, on camera

Run `One_bad_weight_registers_none_of_them` and read the raw body, because this is exactly what bit
episode 22:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "copies[2].baselineWeightInGrams": [
      "A baseline weight is in grams, between 1 and 50000. Weigh the box while it is known complete."
    ]
  },
  "traceId": "00-…"
}
```

`copies[2].baselineWeightInGrams` — camelCase on both segments, index in the middle. That works
because episode 22's `ValidatorOptions.Global.PropertyNameResolver` is applied to **every** member in
the chain, including the one inside the child validator. It is worth pausing on: the mutable global
static episode 22 apologised for is the reason this key is usable, and a `RuleForEach` was the case
it was quietly paying for.

---

## Step 7 — Three validators make scanning worth it

Not test-driven: a package reference is configuration.

```xml
<!-- Directory.Packages.props — with the other PackageVersion entries, alphabetically after FluentValidation -->
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
```

```xml
<!-- src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj — with the other PackageReference entries -->
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
```

The version matches `FluentValidation` exactly, because the two ship in lockstep — check it against
whatever `FluentValidation` is pinned to when you record rather than copying `12.1.1` from here.
Central package management is why the version appears once and the reference does not repeat it,
which is episode 11's work still paying.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — as step 6 left it
builder.Services.AddScoped<IValidator<CatalogueSetRequest>, CatalogueSetRequestValidator>();
builder.Services.AddScoped<IValidator<LookupRequest>, LookupRequestValidator>();
builder.Services.AddScoped<IValidator<RegisterCopiesRequest>, RegisterCopiesRequestValidator>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — replace all three
// Four validators now — three requests and one item — and the fourth was nearly forgotten while
// writing this line, which is the argument. See below for what this costs.
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);
```

**Caveman version:** before, write name of every validator by hand. Four validator now. Hand get
tired, hand forget one. Let computer find them.

### This is the deferral rule paying out in the other direction

Episode 22 introduced FluentValidation and deliberately registered its one validator by hand, and
said why: **explicitness is cheap at one.** Episode 21 refused `Asp.Versioning` on the same
principle. It would be easy to read this course as *never adopt the convenient thing*, and that is
not the rule. The rule is *adopt it when the problem it solves is on screen*.

The problem is now on screen. Four validators, one of them a child that is never resolved from DI at
all, and a fifth arrives in episode 31 with retirement. Three lines becomes five becomes eight, and
every one of them is a line whose only job is to repeat a name that is already written on a class.

**And the price, said plainly, because it is a real one:**

| | By hand | Scanned |
| --- | --- | --- |
| Finding what is registered | Read `Program.cs` | Search the assembly, or trust it |
| A validator nobody registered | Fails at startup? No — fails at the first request, loudly | Silently registered, which is what you wanted |
| A validator nobody *uses* | Obvious: a line in `Program.cs` for a type nothing validates | Invisible. It is registered, resolved by nobody, and dead |
| Startup cost | Zero | One assembly scan, once, measured in milliseconds |

Row three is the one to watch for. **Scanning does not make registration correct, it makes
registration invisible**, and the failure it introduces is a validator that exists, compiles, is
registered, and is never invoked — because the request type it validates was renamed and nothing
pointed at the mismatch. Step 8's filter makes that specific failure impossible for the three
request validators, which is a reason to do the two together.

`ServiceLifetime.Scoped` is passed explicitly rather than taking the default, which is also Scoped.
It is one word and it means a reader does not have to go and look up what the default was.

---

## Step 8 — Refactor: no handler calls a validator by hand

Three handlers currently open the same way:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — and the same five lines in CatalogSetEndpoints and CatalogLookupEndpoints
        ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }
```

Fifteen identical lines, plus three `IValidator<T>` parameters that exist only to feed them. This is
not a size problem — it is that **every new endpoint from here on has to remember them**, and the
failure mode of forgetting is an endpoint that silently accepts anything. Nothing goes red. Nothing
logs. The rules are simply not applied.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/ValidationFilter.cs — new file
using FluentValidation;
using FluentValidation.Results;

namespace BrickShare.Catalog.Api.Endpoints;

/// <summary>
/// Validates the single <typeparamref name="TRequest"/> argument of an endpoint before its handler
/// runs, and answers a 400 with RFC 9457 if it does not pass.
/// </summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Resolved from RequestServices rather than through a constructor. A validator is scoped and
        // a filter is shared machinery, and pulling a scoped service in per request is the version
        // of this that has no lifetime question to answer.
        IValidator<TRequest> validator =
            context.HttpContext.RequestServices.GetRequiredService<IValidator<TRequest>>();

        TRequest? request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        // No argument of that type means the filter was attached to the wrong endpoint. Throwing is
        // right: the alternative is letting every request through unvalidated and never saying so.
        if (request is null)
        {
            throw new InvalidOperationException(
                $"This endpoint has no {typeof(TRequest).Name} argument to validate. "
                + "Check the AddEndpointFilter call against the handler's parameters.");
        }

        ValidationResult validation =
            await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        return validation.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(validation.ToDictionary());
    }
}
```

Attach it at each `MapPost`, and take the parameter and the block out of each handler:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — replace the MapPost line
        group.MapPost("/", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterCopiesRequest>>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replace the MapPost line
        group.MapPost("/", CatalogueAsync)
            .AddEndpointFilter<ValidationFilter<CatalogueSetRequest>>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — replace the MapPost line
        group.MapPost("/", LookUpAsync)
            .AddEndpointFilter<ValidationFilter<LookupRequest>>();
```

Then, in all three handlers: delete the `IValidator<…> validator` parameter, delete the five-line
block, delete the now-unused `using FluentValidation;` and `using FluentValidation.Results;`, and
**drop `ValidationProblem` from the return union** — the handler can no longer produce one.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the signature after the cut
    private static async Task<Results<Created<RegisterCopiesResponse>, ProblemHttpResult>>
        RegisterAsync(
            Guid setId,
            RegisterCopiesRequest request,
            ILabelCodeMinter minter,
            CatalogDbContext database,
            CancellationToken cancellationToken)
    {
        bool setExists = await database.Sets.AnyAsync(set => set.Id == setId, cancellationToken);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — the signature after the cut
    private static async Task<Results<Created<CatalogSetResponse>, ProblemHttpResult>>
        CatalogueAsync(
            CatalogueSetRequest request,
            CatalogDbContext database,
            CancellationToken cancellationToken)
    {
        RebrickableSnapshot? snapshot =
            await database.Snapshots.FindAsync([request.LookupId], cancellationToken);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — the signature after the cut
    private static async Task<Results<Created<LookupResponse>, ProblemHttpResult>> LookUpAsync(
        LookupRequest request,
        IRebrickableCatalog rebrickable,
        CatalogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        SetNumber number = SetNumber.Parse(request.SetNumber);
```

Whole suite, green, with no test changed. **That is what a refactor is** — and it is worth saying,
because three episodes of this course have now used the word and this is the cleanest example: the
behaviour is identical, every test that passed still passes, and none of them were touched.

**Caveman version:** guard used to stand inside every room. Now guard stand at door. Guard never
forget. New room get guard by saying so once.

### What this costs, and where it is paid

**`Results<…>` no longer mentions `ValidationProblem`, so a `400` is now invisible to the type
system.** Today nothing reads those types except the compiler, so nothing breaks. Episode 32 reads
them — it generates OpenAPI from exactly this metadata — and a document that does not mention `400`
on a validated endpoint is a document that lies. The fix there is
`.ProducesValidationProblem()` next to the filter, and it is a hand-maintained claim, which is
precisely the honest note episode 32 already has on its list. **This episode makes that note more
true, and that is the trade.**

**The filter is attached per endpoint, not to the group.** `group.AddEndpointFilter<…>()` would apply
it to every route in the group, which is one route today and is tempting. It is wrong the moment
episode 34 adds `GET /catalog/sets/{setId}/copies` — a `GET` with no body, and a filter that throws
`InvalidOperationException` because there is no `RegisterCopiesRequest` in the argument list.
**The filter is typed to a request, so it belongs where that request does.**

---

## Step 9 — Run it

Not test-driven: a request collection is not code. Replace episode 29's requests 7–11.

```http
### BrickShare.Catalog.Api.http — replace requests 7 to 11

@setId = paste-the-id-from-request-2

### 7. The delivery that arrived: four Titanics, four weights, one request, one transaction.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{
  "copies": [
    { "grade": "New", "baselineWeightInGrams": 9200 },
    { "grade": "New", "baselineWeightInGrams": 9187 },
    { "grade": "New", "baselineWeightInGrams": 9210 },
    { "grade": "New", "baselineWeightInGrams": 9196 }
  ]
}

### 8. One box. Still a delivery, just a short one.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{ "copies": [ { "grade": "Good", "baselineWeightInGrams": 9150 } ] }

### 9. Four boxes, one of them never weighed. 400 — and count(*) does not move.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{
  "copies": [
    { "grade": "New", "baselineWeightInGrams": 9200 },
    { "grade": "New", "baselineWeightInGrams": 9187 },
    { "grade": "New", "baselineWeightInGrams": 0 },
    { "grade": "New", "baselineWeightInGrams": 9196 }
  ]
}

### 10. An empty van. 400, one error against the envelope.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{ "copies": [] }

### 11. A set nobody catalogued. Still 404: the id is in the path, so the resource really is absent.
POST {{host}}/api/v1/catalog/sets/01931f3c-0000-7000-8000-000000000000/copies
Content-Type: application/json

{ "copies": [ { "grade": "New", "baselineWeightInGrams": 9200 } ] }

### 12. Labels the client tried to choose. 201 — and not one of them is in the response.
POST {{host}}/api/v1/catalog/sets/{{setId}}/copies
Content-Type: application/json

{
  "copies": [
    { "grade": "Good", "baselineWeightInGrams": 9150, "labelCode": "BRK-AAAAAA" },
    { "grade": "Good", "baselineWeightInGrams": 9155, "labelCode": "BRK-AAAAAB" }
  ]
}
```

The shot to record is **9**, with a `select count(*) from copies;` immediately before and after.
Same number both times. Say the number out loud.

```sql
select label_code, grade, baseline_weight_grams from copies order by baseline_weight_grams;
```

Then request 12 as the closer, for the same reason episode 29 closed on its version: the client sent
labels, got a `201`, and the response carries codes it did not choose — because `CopyToRegister` has
no `LabelCode` member and the extra JSON is read by nobody. Episode 28's rule, now applied per item
in a list.

---

## What this episode is not

**No partial success.** There is no `207`, no per-item result array, no "fifteen registered, two
rejected". That shape is a real one and there are APIs that need it — but it is the exact state this
episode exists to abolish, and offering it as a *feature* would mean every client has to write the
reconciliation loop that step 1 showed going wrong. If a box is mis-weighed, the staff member fixes
the number and sends the delivery again. Nothing was written, so there is nothing to undo.

**No idempotency key.** Send request 7 twice and eight copies exist. There is no way for the server
to tell a resend from a second delivery, because **a physical box carries no identity until
BrickShare gives it one** — that is the entire reason label codes are minted. The procedural control
is that labels are printed and stuck on at registration, so a box with a sticker has been registered.
Real, and thin; an `Idempotency-Key` header belongs with the payments module, where the consequence
of a double submit is money.

**No `GET`.** Still nothing lists the copies of a set, which is why request 7's `Location` header
points at a route that does not exist. Episode 34.

**No retire.** `Copy.Retire` has been unit-tested since episode 15 and still has no HTTP in front of
it. Next episode.

**No batch anywhere else.** `POST /catalog/sets` still catalogues one set, and `POST /catalog/lookups`
still looks up one number. Neither has a delivery behind it — a shop catalogues a set once, ever —
so batching them would be symmetry for its own sake.

**No authorization.** Anybody who can reach this service can register a hundred copies at a time
now, which is a slightly larger version of the same hole episode 21 named. **Episode 38.**

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green — six in `RegisterCopiesTests`, everything else untouched |
| `dotnet ef migrations has-pending-model-changes` | No changes. `LabelCode.MaxLength` is still 10 |
| `grep -rn "ValidateAsync" src/` | One hit: `ValidationFilter.cs` |
| `grep -rn "IValidator<" src/…/Endpoints/*Endpoints.cs` | Nothing |
| `.http` request 7 | `201`, four copies, four distinct `BRK-` labels, in the order sent |
| `.http` request 9, with `count(*)` either side | `400`, key `copies[2].baselineWeightInGrams`, and the count does not move |
| `.http` request 10 | `400`, key `copies` |
| `.http` request 11 | `404`, `application/problem+json`, a `traceId` extension |
| `.http` request 12 | `201`, and neither `BRK-AAAAAA` nor `BRK-AAAAAB` is in `copies` |
| EF SQL logging on request 7 | One `BEGIN`, four inserts, one `COMMIT` |

The last row is the one to run on camera. It is the only check here that shows the property the
episode is named after, and it shows that **nobody had to write it** — the transaction was always
there, wrapped around whatever one `SaveChangesAsync` happened to contain. The bug was never a
missing `BEGIN`. It was four of them.

## Next

[Episode 31 — Retire is not delete](episode-31.md): the first
endpoint addressed by copy rather than by set, and the first that changes a copy instead of creating
one. Retiring a copy that is out on rent is refused by a rule written in episode 15, turned into a
`409` by a handler written in episode 23, with nothing added to the endpoint to make either happen —
which is what building those two things properly bought.

The sentence to carry out of this one: **the unit of work is the delivery, not the box.** Four boxes
came off one van, and either the system knows about all four or it knows about none of them, because
the one thing it must never do is disagree with the shelf and not know it.
