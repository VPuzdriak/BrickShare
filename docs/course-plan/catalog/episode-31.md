# Episode 31 — Retire is not delete

← [Course plan](catalog-api.md) · Previous: [Episode 30 — All of them or none of them](episode-30.md)

Episode 30 ended on a list of things the catalog still could not do, and one of them had been
waiting a very long time:

> **No retire.** `Copy.Retire` has been unit-tested since episode 15 and still has no HTTP in front
> of it. Next episode.

This is that episode, and it is the smallest feature episode in the module. That is not an accident
and it is not filler. `Copy.Retire` was written in episode 15. The rule that a copy out on rent
cannot be retired was written in episode 15. The handler that turns a refused rule into a `409` was
written in episode 23. The foreign key that refuses to cascade a delete was written in episode 29.
**Four episodes of groundwork, and the feature on top of them is one route, one lookup and one
method call.**

So the episode is about two things. The design rule — **retire is a state change, never a delete** —
and the payoff: two of the four tests here go green the moment they are written, with no production
code, because the work was already done properly somewhere else.

**Done when** a retired copy is still a row, still has its label, and cannot be retired twice.

> **Runtime: about 12 minutes.** Under budget, deliberately, after episode 30 ran to seventeen. If
> it runs short, the place to spend the time is step 1 — the verb table is the part of this episode
> that students will actually argue about.

## Before recording

- Episode 30 merged: `POST /catalog/sets/{setId}/copies` registers a batch, and the six tests in
  `RegisterCopiesTests.cs` are green.
- `docker compose up` for Postgres, `dotnet user-secrets` still holding your Rebrickable key.
- A branch. **No migration this episode** — `retired_at` has been in the `copies` table since
  `InitialCatalog`, episode 16. Have `Migrations/20260821222411_InitialCatalog.cs` open at line 22
  to prove it on camera; the surprise that the column is already there is worth ten seconds.
- `docs/architecture/catalog.md` open at *"Retire is a state change"*, and `docs/IDEA.md` open at
  UC-1.3. Both were written long before this code, and both say the same thing this episode builds.

**Two of the seven steps are not driven by a test, and both say so where they appear**: step 1 is an
argument with a `psql` prompt in it, and step 7 is a request collection. **Two more contain tests
that pass the moment they are written** — steps 5 and 6 — and they say so too, because a test that
describes a rule somebody else already enforced is still a test worth keeping, and pretending it was
red would be a lie told on camera.

Every sample below names its file and where in it the code goes. Where something is edited in a file
that already exists, the **first block is what is already there** — the anchor to find on screen —
and the **second block is what to paste**. Every block is copy-paste clean: no markers, no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/IntegrationTests/Copies/RetireCopyTests.cs` | **New** — four tests and two helpers |
| `src/…/Api/Endpoints/CopyEndpoints.cs` | A second route group, one handler |
| `src/…/Api/Program.cs` | One line next to `v1.MapCopies()` |
| `src/…/Api/BrickShare.Catalog.Api.http` | Requests 13–16 appended |

Nothing in `Domain/`. Nothing in `Persistence/`. No migration. **That list is the episode's
argument** — put it on screen at the start and again at the end.

---

## Step 1 — The delete that eats the history

No code in this step. `docker compose up`, register a copy from episode 30's request 8, and then do
the obvious thing at a `psql` prompt.

```sql
select id, label_code, status from copies;

delete from copies where label_code = 'BRK-K7M2QX';
```

`DELETE 1`. It worked. The box is worn out, the box is gone from the database, and the shop is
tidy.

Now say what was actually destroyed. That row was the only record that this box ever existed.
Every rental that ever went out on it pointed at that id. Every deposit taken against it, every
damage assessment, every photograph. In a system with rentals in it — the rentals service does not
exist yet, and this is the episode that decides what it will be able to rely on — that `DELETE`
either fails on a foreign key or takes the history with it.

**Caveman version:** box old. Box no good for rent. Not mean box never happen. Box go out fifty
time. Fifty time have money in it. Throw away row, throw away fifty time. Shop forget own past.
Shop need past — tax man ask, customer argue, shop have nothing.

### The counterpart that is already in the code

Open `src/Catalog/BrickShare.Catalog.Api/Persistence/CopyConfiguration.cs` and read the comment
episode 29 left there:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CopyConfiguration.cs — already there, do not change it
        // Deleting the set should not delete the copies.
        // Deleting the set should not happen in principle, but if it does - database should refuse it
        builder.HasOne<CatalogSet>()
            .WithMany()
            .HasForeignKey(copy => copy.CatalogSetId)
            .HasConstraintName("fk_copies_catalog_set_id")
            .OnDelete(DeleteBehavior.Restrict);
```

EF's convention for a required relationship is `Cascade`. Episode 29 overrode it to `Restrict`
without fully explaining why, and this is why: **the database is the last line of defence under a
rule the application is about to make unnecessary.** Once retirement exists, nothing in the service
ever needs to delete a copy — and the schema is configured so that if somebody writes the delete
anyway, Postgres refuses it rather than quietly obeying.

That is the shape worth teaching. A rule that lives only in the domain model is a rule that a
`DELETE` typed into `psql` at eleven at night does not know about.

### Why `POST` to a named sub-resource

The route is `POST /api/v1/catalog/copies/{id}/retirement`. There are two more obvious options and
both are worse.

| | `POST /copies/{id}/retirement` (chosen) | `DELETE /copies/{id}` | `PATCH /copies/{id}` with `{"status": "Retired"}` |
| --- | --- | --- | --- |
| What the request says | A retirement happened to this copy | This copy should not exist | The client has decided the new status |
| Who owns the state machine | The server. The client asks for one named thing | — | The client, which now needs a copy of the rules |
| A refusal reads as | `409` — the shop tried something the rules forbid | A bug. `DELETE` that fails looks broken | `409`, but every transition shares one route and one meaning |
| Adding *write off as lost* later | A second named sub-resource, obviously different | Nothing sensible | Same route, different magic string |

The deciding argument is the third row, not the first. `Copy` has ten transitions on it and this
episode is exposing one. **A route named after the thing that happened leaves room for the other
nine**; a route named after the field being written turns them all into one endpoint whose meaning
depends on the body — and then every validation error, every audit log entry and every permission
check has to re-read the body to find out what was actually asked for.

**Caveman version:** not say "make status word be Retired". Say "this box retire now". Computer
know what retire mean. Computer know when retire not allowed. Client just point at box and say
word.

The noun rather than the verb — `/retirement`, not `/retire` — is the coin-flip part, and worth
admitting as one. `POST /retire` reads as a command and plenty of good APIs spell it that way. The
noun wins here on a thin margin: `POST` creating a *retirement* is consistent with every other
`POST` in this service creating something, and episode 32 is about to generate a document out of
these routes where consistency is what makes the list readable. **That is a style argument, not a
correctness one, and if your team prefers the verb, use the verb.**

> **The architecture document said `/copies/{id}/retire`.** It has been updated to match. When the
> code and the document disagree, one of them is wrong, and it is not automatically the code —
> but it is never acceptable to leave both.

---

## Step 2 — Red: the first endpoint addressed by a copy

Every route in this service so far has started `/catalog/sets/…`. This one does not, and the test
is where that decision shows up first.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RetireCopyTests.cs — new file
using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Copies;

public class RetireCopyTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_retired_copy_is_still_a_row()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid copyId = await RegisterOneCopyAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/catalog/copies/{copyId}/retirement", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // A second connection, because the question is what the database holds and not what some
        // change tracker remembers being told.
        await using CatalogDbContext dbContext = Database.NewDbContext();
        Copy? retired = await dbContext.Copies.SingleOrDefaultAsync(copy => copy.Id == copyId);

        // The three assertions the episode is named after.
        Assert.NotNull(retired);
        Assert.Equal(CopyStatus.Retired, retired.Status);
        Assert.NotNull(retired.RetiredAt);
        Assert.StartsWith("BRK-", retired.Label.Value);
    }

    /// <summary>
    /// One box on the shelf, through the front door. The id comes back in the registration
    /// response because there is still no GET for a copy — episode 34.
    /// </summary>
    private async Task<Guid> RegisterOneCopyAsync(HttpClient client)
    {
        Guid setId = await Database.CatalogueTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { new { grade = "New", baselineWeightInGrams = 9200 } } });

        response.EnsureSuccessStatusCode();

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);

        return Assert.Single(registered.Copies).Id;
    }
}
```

Run it. **`404`, expected `204`.** The route does not exist, so ASP.NET Core answers the only way it
can, and `CLAUDE.md` counts that as a legitimate red — there is nothing to build yet and the test
says exactly what is missing.

**Caveman version:** test knock on door. No door there. Test say "no door". Good. Now make door.

Note what the assertions are and are not. They do not check a response body, because there is not
going to be one. They check the **row**: still present, status changed, timestamp set, label
untouched. The label matters more than it looks — a retired copy keeps its identity because the
sticker is still on the box, and somebody will scan it in two years and deserve an answer better
than *unknown code*.

---

## Step 3 — Green: one route, one lookup, one method call

Two edits. First, a second route group in the file that already owns copy routes.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the anchor, already there
    public static RouteGroupBuilder MapCopies(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog/sets/{setId:guid}/copies")
            .WithTags("Copies");

        group.MapPost("/", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterCopiesRequest>>();

        return group;
    }
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — paste directly below MapCopies
    public static RouteGroupBuilder MapCopyRetirements(this IEndpointRouteBuilder routes)
    {
        // Not under /catalog/sets/{setId}. A copy has its own id, and the caller scanning a label
        // has that id and nothing else.
        RouteGroupBuilder group = routes.MapGroup("/catalog/copies")
            .WithTags("Copies");

        // No validation filter: there is no request body to validate. The whole request is a
        // route parameter, and the route constraint has already rejected anything that is not a
        // Guid before the handler is reached.
        group.MapPost("/{copyId:guid}/retirement", RetireAsync);

        return group;
    }

    private static async Task<NoContent> RetireAsync(
        Guid copyId,
        CatalogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        Copy copy = await database.Copies.SingleAsync(
            candidate => candidate.Id == copyId, cancellationToken);

        copy.Retire(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
```

Then one line in `Program.cs`.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — the anchor, already there
v1.MapCatalogSets();
v1.MapCatalogLookups();
v1.MapCopies();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — what it becomes
v1.MapCatalogSets();
v1.MapCatalogLookups();
v1.MapCopies();
v1.MapCopyRetirements();
```

Green.

**Caveman version:** find box by number. Tell box: retire. Box check own rule, box change own
word, box write down when. Save. Say nothing back — two-oh-four mean "done, no words".

### Three decisions in eleven lines

**Why the group is `/catalog/copies` and not `/catalog/sets/{setId}/copies/{id}`.** The nested route
is tempting because copies already live under sets for registration, and symmetry feels like
tidiness. It is wrong here for a concrete reason: **a staff member retiring a box is holding the
box and scanning its label.** They have a copy id. Making them supply the set id as well means
looking it up first, which means the URL can be *self-inconsistent* — a valid set id and a valid
copy id that have nothing to do with each other — and now the endpoint needs a rule about what to do
about that. A route that cannot express a contradiction does not need a rule for one.

Registration is nested because it genuinely is an operation *on a set*: you are adding to the set's
stock, and the set id is the only id in existence at the time. Retirement is an operation on a copy.
**The nesting follows the operation, not the table.** Episode 34 adds `GET /catalog/copies/by-label/{code}`
to this same group, for the same reason.

**Why `clock.GetUtcNow()` and not `DateTimeOffset.UtcNow` inside `Copy.Retire`.** Episode 15 wrote
`Retire(DateTimeOffset retiredAt)` taking the instant as a parameter, and that is why
`CopyStatusTests` can assert against a fixed instant without any test framework magic. The domain
does not read the clock; the caller does. `TimeProvider.System` is already registered in
`Program.cs` — episode 27 needed it for lookup snapshots — so this costs one constructor parameter
and no new wiring. **A domain model that calls `DateTimeOffset.UtcNow` has a hidden dependency on
the machine it runs on, and the first test that needs "what happens at midnight" pays for it.**

**Why `204` and what it costs.** No body. The staff member's client gets an empty response and has
to believe it, because there is no `GET /catalog/copies/{id}` until episode 34 — so *it cannot
re-read the copy it just retired*. That is a real cost and worth stating plainly rather than
selling `204` as obviously correct. The alternative is returning the copy, which would mean adding
`RetiredAt` to `CopyResponse` and shipping a read model out of a write endpoint before anything has
asked for one.

The call goes to `204` because **the client already knows everything the response would contain**:
it sent the id, the status is the one it asked for, and the timestamp is the moment it called. The
on-camera proof that the row survived is the `psql` query in step 7, and the permanent proof is the
test in step 2 — which is the right place for it, because that assertion is about the database, and
a response body would only be the API's opinion of the database.

---

## Step 4 — Red: a copy nobody registered

The step-3 handler has an obvious hole, so put a test on it rather than fixing it quietly.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RetireCopyTests.cs — a second [Fact], below the first
    [Fact]
    public async Task A_copy_nobody_registered_cannot_be_retired()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/catalog/copies/{Guid.CreateVersion7()}/retirement", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("not registered", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }
```

Add the `using` for `ProblemDetails` at the top of the file:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RetireCopyTests.cs — with the other usings
using Microsoft.AspNetCore.Mvc;
```

Red: **`500`.** `SingleAsync` throws `InvalidOperationException` when the sequence is empty, nothing
claims that exception, and episode 23's pipeline correctly reports it as the server fault it looks
like. Worth pausing on: the `500` is not a bug in the error handling. The error handling did exactly
the right thing with an exception that says *a query returned no rows when the code assumed it
would* — which is the signature of a defect, and here it is one.

Green is a guard.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — RetireAsync, replacing the step-3 version
    private static async Task<Results<NoContent, ProblemHttpResult>> RetireAsync(
        Guid copyId,
        CatalogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        Copy? copy = await database.Copies.FindAsync([copyId], cancellationToken);
        if (copy is null)
        {
            return TypedResults.Problem(
                title: "No such copy",
                detail: $"Copy {copyId} is not registered. Scan the label again, or check the id "
                        + "against the response from the registration that created it.",
                statusCode: StatusCodes.Status404NotFound);
        }

        copy.Retire(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
```

`FindAsync` rather than `SingleOrDefaultAsync`: the argument is a primary key, and `FindAsync` is
the method that says so. It also checks the change tracker before the database, which is free here
and is the correct behaviour if this handler ever grows a second read.

**Caveman version:** box not exist. Not shout "computer broken". Say "no box here". Different word
for different problem. Man reading error need know which one is him fault.

### The same ladder, one rung along

Episode 28 refused an unknown `lookupId` with a `422`. This one refuses an unknown copy id with a
`404`. Same reasoning, different answer, and the difference is **where the id was**:

| | Episode 28 | This episode |
| --- | --- | --- |
| The id was in | the request **body** | the request **path** |
| So the thing that is missing is | a value the request referred to | the resource the request addressed |
| Which means | the request was well-formed and unprocessable — `422` | there is nothing at that URL — `404` |

Say this out loud once, because it is the whole point of having spent episode 23 on a status-code
ladder: **the ladder is reasoning, not a lookup table.** Anyone who memorised "not found means 404"
gets this right by luck; anyone who learned "404 is about the URL" gets the next one right too.

---

## Step 5 — A test that passes the moment it is written

Episode 15 wrote this rule, and it has been green in `CopyStatusTests` ever since:

```csharp
// tests/BrickShare.Catalog.UnitTests/CopyStatusTests.cs — already there, do not change it
    [Fact]
    public void A_copy_that_is_out_on_rent_cannot_be_retired()
    {
        Copy copy = OnRent();

        Assert.Throws<DomainRuleViolationException>(() => copy.Retire(AnyInstant));
    }
```

It has never run through HTTP. Now it does.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RetireCopyTests.cs — a third [Fact]
    [Fact]
    public async Task A_copy_that_is_out_on_rent_cannot_be_retired()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid copyId = await RegisterOneCopyAsync(client);
        await SendOnRentAsync(copyId);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/catalog/copies/{copyId}/retirement", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);

        // The domain wrote this sentence, in episode 15, for a person to read.
        Assert.Contains("OnRent", problem.Detail);
        Assert.True(problem.Extensions.ContainsKey("traceId"));

        // And the refusal is a refusal: nothing was written.
        await using CatalogDbContext dbContext = Database.NewDbContext();
        Copy? unchanged = await dbContext.Copies.SingleOrDefaultAsync(copy => copy.Id == copyId);

        Assert.NotNull(unchanged);
        Assert.Equal(CopyStatus.OnRent, unchanged.Status);
        Assert.Null(unchanged.RetiredAt);
    }

    /// <summary>
    /// Drives a copy to OnRent through the domain rather than through HTTP, because the endpoints
    /// that would do it belong to a rentals service that does not exist yet.
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

**This test is green the first time it is run, and that is worth stopping the recording to say.**

`CLAUDE.md` has a rule for exactly this moment: *some tests drive a design and some describe a rule;
both belong in the suite, and only the first is TDD doing its job.* This is the second kind. Nothing
in `CopyEndpoints.cs` changes. There is no `try`, no `catch`, no status-code mapping, and the
handler contains no knowledge whatsoever that retirement can be refused.

Trace what actually happened when the request came in:

| Where | What | Written in |
| --- | --- | --- |
| `Copy.TransitionTo` | `OnRent` is not in the allowed set, so `DomainRuleViolationException` | Episode 15 |
| `app.UseExceptionHandler()` | Catches it, offers it to the registered handlers | Episode 23 |
| `DomainRuleViolationExceptionHandler` | Recognises the type, writes a `409` with the message as `detail` | Episode 23 |
| `AddProblemDetails` | Adds `instance` and `traceId` | Episode 23 |
| `RetireAsync` | Nothing. It never saw the exception | — |

**Caveman version:** box say no. Box shout. Wall built long ago catch shout, turn shout into
polite letter, number four-oh-nine. New code do nothing. New code not even know shout possible.
This why build wall once, properly.

That is the return on episode 23. A cross-cutting concern built once means the *second* endpoint
that needs it costs nothing, and this episode is where the interest gets paid. The failure mode it
avoids is the one every codebase has a version of: the same `catch (DomainRuleViolationException)`
block copy-pasted into nine handlers, eight of them identical and the ninth subtly different.

### The test reaches around the API on purpose

`SendOnRentAsync` opens a `DbContext` and calls `Reserve()` and `Collect()` directly. That is a test
going behind the public interface, which is normally a smell, and here it is the honest option: the
transitions it needs are requested by **other services** — the architecture document has them
arriving at an internal `POST /copies/{id}/status` — and none of those exist. The alternatives are
worse. Writing SQL to set `status = 'OnRent'` would bypass the state machine and could set up a
state the domain says is impossible; adding a test-only endpoint would ship a hole in production to
make a test convenient.

Going through the domain object keeps the setup **legal by construction**: `Reserve` then `Collect`
is the same path a real rental takes, and if episode 15's rules ever change so that path becomes
invalid, this helper stops compiling or starts throwing. **A test fixture that can only build states
the domain allows is a test fixture that cannot lie to you.**

---

## Step 6 — Retiring twice

Also green on arrival. Also kept.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RetireCopyTests.cs — a fourth [Fact]
    [Fact]
    public async Task A_copy_cannot_be_retired_twice()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid copyId = await RegisterOneCopyAsync(client);

        HttpResponseMessage first = await client.PostAsync(
            $"/api/v1/catalog/copies/{copyId}/retirement", content: null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        DateTimeOffset? retiredAt;

        await using (CatalogDbContext afterFirst = Database.NewDbContext())
        {
            retiredAt = (await afterFirst.Copies
                .SingleAsync(copy => copy.Id == copyId)).RetiredAt;
        }

        HttpResponseMessage second = await client.PostAsync(
            $"/api/v1/catalog/copies/{copyId}/retirement", content: null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // The second call did not move the timestamp. A retirement has a date, and that date is
        // the day it actually happened.
        await using CatalogDbContext afterSecond = Database.NewDbContext();
        Copy still = await afterSecond.Copies.SingleAsync(copy => copy.Id == copyId);

        Assert.Equal(retiredAt, still.RetiredAt);
    }
```

### Why this is a separate test from step 5

They fail in the same method — `TransitionTo`, three lines of it — and it would be reasonable to
call one of them redundant. They are not, because they answer questions asked by different people.

**Step 5 is the business rule.** UC-1.3 says a copy cannot be retired while a rental is active on
it, the shop cares, and the test exists so that the rule cannot be removed silently.

**Step 6 is the question every client integrator asks in week one:** *what happens if my request
times out and I retry?* The answer here is `409`, not `204`, and that is a real API design position
rather than an accident — **this endpoint is not idempotent, and the second caller is told so.** A
client that retries blindly gets a conflict and has to look; a client that meant to retire the box
twice was confused about something.

There is a defensible opposite choice: return `204` on the second call, on the grounds that the
desired state is already the actual state. It is a smaller surprise for retrying clients. It is
rejected here because `Copy.Retire` cannot express it — the state machine refuses the transition and
the endpoint would have to special-case `Retired` before calling the domain, which puts a rule in
the endpoint that belongs in the model. **When making the API friendlier means moving a rule out of
the domain, the API stays unfriendly.**

**Caveman version:** man retire box. Man retire box again. Computer say no, already done, here the
day it happen. Computer not change day. Day is day.

Run the suite. **Four green in `RetireCopyTests`, and every other test in the repository untouched.**

---

## Step 7 — Run it

Not test-driven: a request collection is not code. Append to the end of `BrickShare.Catalog.Api.http`,
after episode 30's request 12.

```http
### BrickShare.Catalog.Api.http — append after request 12

@copyId = paste-an-id-from-the-response-to-request-7

### 13. Retire a copy. 204, empty body — and the row does not go anywhere.
POST {{host}}/api/v1/catalog/copies/{{copyId}}/retirement

### 14. The same copy, again. 409 — and the retired_at from request 13 does not move.
POST {{host}}/api/v1/catalog/copies/{{copyId}}/retirement

### 15. A copy nobody registered. 404: the id is in the path, so the resource really is absent.
POST {{host}}/api/v1/catalog/copies/01931f3c-0000-7000-8000-000000000000/retirement

### 16. A copy that is out on rent. Run the update below first, then this. 409.
POST {{host}}/api/v1/catalog/copies/{{copyId}}/retirement
```

Request 16 needs a copy in `OnRent`, and nothing in this service can put one there yet. For the demo
only, at the `psql` prompt — and say on camera that this is the one thing in the episode that cheats,
and why it is allowed to: the rentals service that will make this transition legitimately is not
written yet.

```sql
update copies set status = 'OnRent' where id = '<the id from request 8>';
```

Then the shot the episode is named after, with request 13 immediately before it:

```sql
select label_code, status, retired_at from copies order by retired_at nulls last;
```

The retired box is **still on the list**. Same label, status `Retired`, a timestamp next to it, and
every other column exactly as it was. Say the sentence: *nothing was deleted, and the shop can still
answer a question about this box in five years.*

Then, for contrast, try the thing step 1 did, on a copy that has a set behind it:

```sql
delete from catalog_sets where id = '<the set id from request 2>';
```

`ERROR: update or delete on table "catalog_sets" violates foreign key constraint
"fk_copies_catalog_set_id" on table "copies"`. **Episode 29's `Restrict`, doing its job, on camera,
two episodes after it was written.**

---

## What this episode is not

**No un-retire.** `Retired` is terminal — `TransitionTo` has no path out of it, `IDEA.md` says
terminal states end subscriptions, and there is no `Unretire` method to expose. If a shop retires
the wrong box, today the answer is a support ticket and a SQL statement. That is a genuinely
unsatisfying answer and it is the correct one for now: the right fix is a **correction** with an
audit trail behind it, not an undo button, and designing that properly is not a ten-minute episode.

**No retirement reason.** *Worn out*, *incomplete*, *damaged by a customer* — the shop will want to
know, UC-10.5 and the deposit rules will eventually need it, and it is deliberately not here. It is
not free: a reason is a new column, a migration, an enum with a wire format and a validation rule,
and nothing in the system reads it yet. **A field nobody reads is a field nobody maintains, and it
will be full of `Other` by the time something needs it.** It arrives with the episode that has a
reader for it.

**No `CopyRetired` event.** The architecture document is clear that retiring a copy has to end
subscriptions and tell the subscribers — that is UC-3.6. It is not here because there is no
subscriptions service to tell, and the whole outbox-and-events story is staged for after a second
service exists, exactly as `catalog-api.md` says. Publishing an event into a void teaches the
mechanics and hides the reason.

**No filtering.** Nothing excludes retired copies from anything, because nothing lists copies.
Episode 34 builds the listings, and *retired copies are not shown to customers* is a rule that
belongs in the episode where there is a customer-facing list to leave them out of.

**No concurrency test.** `CopyConfiguration` maps `xmin` as a row version, so two staff members
retiring the same box at the same instant produce a `DbUpdateConcurrencyException` and a `500`. The
mapping is right and the response is not. Episode 40 turns it into a `409`.

**No authorization.** Anyone who can reach this service can retire any box in the shop by guessing
a `Guid`, which they cannot, which is not a security control. **Episode 38.**

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green — four new in `RetireCopyTests`, nothing else changed |
| `dotnet ef migrations has-pending-model-changes` | No changes. `retired_at` has been in the schema since `InitialCatalog` |
| `git diff --stat` | Four files. None of them in `Domain/` or `Persistence/` |
| `grep -rn "409\|Conflict" src/Catalog/BrickShare.Catalog.Api/Endpoints/` | One hit, and it is a comment in `CatalogSetEndpoints.cs` saying why *that* endpoint is not a `409`. No endpoint writes one |
| `grep -rn "Remove\|ExecuteDelete" src/Catalog/BrickShare.Catalog.Api/` | Nothing. The service has no delete in it |
| `.http` request 13 | `204`, empty body |
| `.http` request 14 | `409`, `application/problem+json`, `traceId`, and `retired_at` unchanged |
| `.http` request 15 | `404`, and a `detail` that says what to do |
| `.http` request 16 | `409`, mentioning `OnRent` |
| `select … from copies` after 13 | The row is there, with its label |
| `delete from catalog_sets …` | Postgres refuses it |

Rows five and six are the ones to run on camera. **The status code this episode is most about does
not appear in the code this episode wrote, and neither does the operation it is named after** —
there is no delete anywhere in the service, and there never was one to remove.

## Next

[Episode 32 — What this API says about itself](episode-32.md):
the OpenAPI document, generated from the C# types rather than hand-written and immediately stale.
Every endpoint built since episode 21 is about to be described by a document that nobody wrote — and
the places where the types cannot express the truth, including the `409` this episode never typed
and the `400` episode 30 removed from its return union, are where the honest work is.

The sentence to carry out of this one: **the shop stops renting the box; the system does not stop
knowing about it.** A row that records something that happened is not a row you get to delete
because the thing it records is over.
