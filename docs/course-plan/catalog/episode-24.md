# Episode 24 — Two people, one Titanic

← [Course plan](catalog-api.md) · Previous: [Episode 23 — A refused rule is not a bug](episode-23.md)

The shop owns three Titanics. UC-1 is firm about what that means:

> The shop might own three Titanics. That is **one** catalog entry and **three** copies: the specs
> are described once, and the copies carry only what actually differs between boxes.

Nothing stops a second person creating a second entry. This episode closes the last of the three
gates, and it is the interesting one, because **it cannot be closed in C#**.

**Done when** cataloguing the same set number twice comes back `409` through the same handler
episode 23 built, `select count(*)` says 1, and the `.http` file documents all three refusals.

## Before recording

- Episode 23 merged: `DomainRuleViolationException`, the handler, 409s working.
- `docker compose up`, and a REST client for the `.http` file.
- A branch.
- [`episode-16.md`](episode-16.md) open at the label-code uniqueness argument. This episode is that
  argument arriving at an HTTP status code, and the sentence it wrote then is the sentence to read
  out now.

**Step 1 is red-green.** Step 2 is a `.http` file, which is neither code nor tested and does not
pretend to be.

---

## Step 1 — Red

```csharp
    [Fact]
    public async Task A_set_that_is_already_catalogued_cannot_be_catalogued_again()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await using CatalogDbContext database = Database.NewDbContext();
        Assert.Equal(1, await database.Sets.CountAsync());
    }
```

```
Expected: Conflict
Actual:   InternalServerError
```

A 500 again, and this time from `SaveChangesAsync` — a `DbUpdateException` wrapping
`23505: duplicate key value violates unique constraint "ix_catalog_sets_set_number"`. **The database
doing its job correctly and the API doing its job badly**, exactly as in episode 23.

---

## Step 2 — Green, and the fix that is not the obvious one

```csharp
        database.Sets.Add(set);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (IsAlreadyCatalogued(error))
        {
            throw new DomainRuleViolationException(
                $"Set {set.Number} is already catalogued. Register another copy against the "
                + "existing entry instead of creating a second one.",
                error);
        }
```

```csharp
    // Matches the one constraint we mean. A different unique violation is a different bug and
    // must not be reported to staff as "you have already catalogued this".
    private static bool IsAlreadyCatalogued(DbUpdateException error) =>
        error.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_catalog_sets_set_number"
        };
```

with `using Microsoft.EntityFrameworkCore;` and `using Npgsql;` added. Green — and **episode 23's
third constructor has just earned itself**: `error` goes in as the inner exception, so the Postgres
detail stays in the logs while the client sees a sentence about LEGO.

```
HTTP/1.1 409 Conflict

{
  "title": "The catalog refused this change.",
  "status": 409,
  "detail": "Set 10294-1 is already catalogued. Register another copy against the existing entry instead of creating a second one.",
  "instance": "/api/v1/catalog/sets"
}
```

**No new handler, no new status code, no new anything.** One exception type, three causes now — a
domain rule, a database constraint, and whatever episode 27 adds next. That is what a well-chosen
seam looks like from the far side.

### Why not check first

The obvious implementation is a query:

```csharp
// Not this.
if (await database.Sets.AnyAsync(s => s.Number == number, cancellationToken))
{
    throw new DomainRuleViolationException(…);
}
```

It reads better, it produces the same message, and **it is a race**. Two staff members catalogue the
same set at the same moment. Both queries return false. Both inserts run. One of them still fails on
the unique index — so the check has not removed the failure path, it has only made it rarer, which
is worse than not having it: a bug that happens on one deployment in fifty is a bug nobody can
reproduce and nobody gets to fix.

Episode 16 wrote the sentence this is the payoff for:

> an application check is a race, and a unique index is an arbiter

**The database is the only participant that sees both transactions.** Anything checking outside it
is guessing about a moment that has not happened yet.

Two things worth adding while the code is on screen:

- **A pre-check is a legitimate optimisation, not a correctness measure.** If cataloguing were
  expensive — a Rebrickable call and an image copy, which is exactly what episodes 25 and 26 add —
  failing fast before spending it is worth doing. It just has to be *in addition to* the catch,
  never instead of it.
- **The `when` filter is doing real work.** `catch (DbUpdateException)` alone would report *any*
  write failure as "already catalogued": a foreign key violation, a check constraint, a concurrency
  conflict. Episode 27 adds a second unique constraint to this service and that catch block would
  start lying about it. Naming the constraint keeps the claim as narrow as the evidence.

---

## Step 3 — The contract, committed

`src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.http` has held three GETs since episode
3 and gains the interesting part:

```http
### Catalogue a set — the happy path
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{
  "setNumber": "10294-1",
  "name": "Titanic",
  "theme": "Icons",
  "year": 2021,
  "pieceCount": 9090,
  "retailPrice": 629.99,
  "baseRentalPrice": 60.00,
  "minimumRentalDays": 7,
  "minimumAge": 18
}

### Refused at the edge — nonsense, field by field. Expect 400.
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{
  "setNumber": "", "name": "", "theme": "", "year": 0, "pieceCount": 0,
  "retailPrice": -5, "baseRentalPrice": 0, "minimumRentalDays": 0, "minimumAge": 99
}

### Refused by the domain — a reasonable request the shop does not allow. Expect 409.
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{
  "setNumber": "75192-1", "name": "Millennium Falcon", "theme": "Star Wars", "year": 2017,
  "pieceCount": 7541, "retailPrice": 849.99, "baseRentalPrice": 80.00,
  "minimumRentalDays": 30, "minimumAge": 16
}

### Refused by the database — send the happy path twice. Expect 201, then 409.
```

Run them against `docker compose up`. **Three of the four fail on purpose, and that is why the file
is worth committing.** A `.http` file holding only successful calls documents the happy path; one
holding the refusals documents the *contract*, and the next person to change this endpoint finds out
in four keystrokes whether they broke it.

### The block, finished

The diagram episode 20 opened with, now with all three gates closed and a test behind each:

```
   request ──▶ [ edge validation ]──▶ [ domain rules ]──▶ [ database constraints ]
                      │                     │                      │
                     400                   409                    409
                   ep 22                  ep 23                  ep 24
```

Four tests in `CatalogueSetTests` — created, edge-refused, domain-refused, database-refused. **A
reader who opens that file in a year should be able to see the design in the test names**, and if a
fifth gate ever appears there should be a fifth test.

---

## What this episode is not

**No optimistic concurrency on `catalog_sets` in anger.** The `xmin` row version is mapped, as it is
on `copies`, and nothing updates a set yet, so nothing has exercised it. `PATCH /catalog/sets/{id}`
is where that becomes a real test.

**No retry.** A 409 here is final: the client must not re-send, because the set genuinely exists.
That is the opposite of the concurrency conflict episode 16 mapped, where retrying *is* the answer —
and the two arriving as different status codes is what lets a client tell them apart.

**No OpenAPI document.** Three status codes now exist and nothing describes them. Episode 28, once
there are enough endpoint groups for a document to be worth generating.

**Still no authorization.** Five episodes in, the endpoint is open. Episode 33.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green — four tests in `CatalogueSetTests`, one per gate |
| The same `setNumber` twice | `201`, then `409`, and `select count(*) from catalog_sets` is 1 |
| The 409 body | `detail` naming the set number, not a Postgres error |
| The log for that request | Still carries the `PostgresException` as the inner exception |
| `catch (DbUpdateException)` without the `when` filter | Every write failure becomes "already catalogued". Try it, then put it back |
| The `.http` file, all four requests | `201`, `400`, `409`, `409` |
| The same four against Azure | Identical |

## Next

[Episode 25 — Talking to Rebrickable](catalog-api.md#episode-25--talking-to-rebrickable):
the endpoint these five episodes built believes whatever it is told, and the next two are about
where the truth actually comes from.

Episode 25 builds the typed `HttpClient` — timeout, retry with backoff and circuit breaker via
`Microsoft.Extensions.Http.Resilience`, and what each one is protecting against, because retry
without a circuit breaker turns a slow dependency into a self-inflicted outage. It is also where
**Key Vault appears**, at the moment there is finally a genuine secret to keep: the Rebrickable API
key. Every credential in this system so far has been a managed identity, and that streak was the
point — it ends here because a third party's API key is a real secret with nowhere else to go.

Then episode 26 takes the request body these five episodes built and deletes most of it.
