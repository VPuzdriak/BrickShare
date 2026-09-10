# Episode 23 — A refused rule is not a bug

← [Course plan](catalog-api.md) · Previous: [Episode 22 — Refusing nonsense at the edge](episode-22.md)

Episode 22 ended with one request still returning 500. Here it is:

```bash
curl -i -X POST http://localhost:5080/api/v1/catalog/sets \
  -H 'Content-Type: application/json' \
  -d '{ "setNumber": "10294-1", "name": "Titanic", "theme": "Icons", "year": 2021,
        "pieceCount": 9090, "retailPrice": 629.99, "baseRentalPrice": 60.00,
        "minimumRentalDays": 30, "minimumAge": 18 }'
```

**Read the request back before explaining anything.** A member of staff has catalogued a nine
thousand piece set and said the minimum rental is a month. Every field is well-formed. Every value
is plausible. A shop that rented large sets by the month would type exactly this.

```
HTTP/1.1 500 Internal Server Error
content-type: application/problem+json

{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "instance": "/api/v1/catalog/sets",
  "traceId": "00-967739cc…-00"
}
```

**Machine-readable, consistent with episode 22's `400` — and a lie.** It says the server failed. The
server did exactly what it was told, by a rule written in episode 20, and had no way to say so.

The exception message is absent too, and that default is right: exception text leaks table names,
file paths and internal structure to whoever asked. **The framework cannot tell that this particular
message was written for a human, because nothing in the code says so** — which is the whole
diagnosis.

**Done when** a refused business rule comes back `409` carrying the domain's own sentence, a genuine
defect still comes back `500`, and the twelve assertions in the episode 14 and 15 test files have
been refactored on camera to say which of the two they mean.

## Before recording

- Episode 22 merged: the FluentValidation validator, `AddProblemDetails` and `UseExceptionHandler`.
- A branch.
- [`episode-14.md`](episode-14.md) and [`episode-15.md`](episode-15.md) open. Twelve assertions
  across their two test files change today, and it is worth having the originals on screen.

**Step 2 is a refactor driven from the test side**, which is a shape this course has not shown yet:
the assertions change first, the suite goes red in a way that has nothing to do with the endpoint,
and only then does `Copy.cs` change. That is what "the tests are the specification" looks like when
the specification is about a *type* rather than a value.

---

## Step 1 — Why the type is the problem

`Copy.Regrade` throws `InvalidOperationException`. So does `Copy.Retire` on a copy that is out on
rent. So does `CatalogSet.Catalogue`. And so does:

- `List<T>` when the collection is modified during a `foreach`
- `Task` when it is awaited a second time
- `DbContext` when it is used after disposal
- `IEnumerable.First()` on an empty sequence

Four bugs and three business rules, arriving at the API layer as **the same type**. There is no
predicate an exception handler can write that separates them, and both available options are wrong:

| | What it does | Why it is wrong |
| --- | --- | --- |
| Map `InvalidOperationException` → 409 | Business rules get a good response | So does a disposed `DbContext`. A genuine bug is reported to the client as *your request conflicts with something*, and the error-rate dashboard stays green through an outage |
| Map it → 500 | Bugs are reported honestly | So are business rules. Which is where we are now |

**Neither is a coding mistake. The type is simply not carrying the information.** So give it some.

### Red

```csharp
    [Fact]
    public async Task A_minimum_rental_period_the_shop_cannot_honour_is_refused()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(minimumRentalDays: 30));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("28", problem.Detail);
    }
```

with `using Microsoft.AspNetCore.Mvc;` for `ProblemDetails`, and `ValidRequest` gaining a
`minimumRentalDays` parameter.

```
Expected: Conflict
Actual:   InternalServerError
```

---

## Step 2 — A type that means "the business said no"

`src/Catalog/BrickShare.Catalog.Domain/DomainRuleViolationException.cs`:

```csharp
namespace BrickShare.Catalog.Domain;

/// <summary>
/// A business rule refused an operation. The caller asked for something legal-looking that this
/// domain does not permit — not a defect, and never to be reported as one.
///
/// The message is written for a person to read, because episode 23 puts it on the wire.
/// </summary>
public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException()
    {
    }

    public DomainRuleViolationException(string message)
        : base(message)
    {
    }

    public DomainRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

**Two things about that file are worth a sentence each.**

The three constructors are CA1032, and the analyzer is right for a boring reason: an exception type
missing the standard set surprises everything that reflects over exceptions — serializers, loggers,
`Activator.CreateInstance`. Two will never be called by this codebase and they cost four lines.
Episode 10's `TreatWarningsAsErrors` means this is not optional, which is episode 10 doing precisely
the job it was introduced to do.

**It derives from `Exception`, not from `InvalidOperationException`**, and that is a real choice.
Deriving would keep every existing `catch (InvalidOperationException)` working, which sounds like a
kindness and is the opposite: the entire purpose of this type is that it is *not* one of those, and
inheriting means any handler broad enough to catch the parent silently swallows it again. **A type
introduced to draw a distinction must not be assignable to the thing it is distinguished from.**

### The refactor, driven from the tests

Twelve assertions across two files currently say `Assert.Throws<InvalidOperationException>`. Change
them first, before touching `Copy.cs`:

```csharp
// tests/BrickShare.Catalog.UnitTests/CopyGradeTests.cs — five of them
        Assert.Throws<DomainRuleViolationException>(() => copy.Regrade(ConditionGrade.Good));

// tests/BrickShare.Catalog.UnitTests/CopyStatusTests.cs — seven of them
        Assert.Throws<DomainRuleViolationException>(copy.Collect);
```

```
Failed!  - Failed: 12
Assert.Throws() Failure: Exception type was not an exact match
Expected: BrickShare.Catalog.Domain.DomainRuleViolationException
Actual:   System.InvalidOperationException
```

Twelve red tests, and **not one of them is about the new endpoint**. A change to the contract came
from the specification side, the suite objected in exactly twelve places, and the list of places is
the list of business rules this domain enforces. Nobody had to go looking for them.

Now `Copy.cs` — the two throws in the grade methods and the one in `TransitionTo`:

```csharp
    private void TransitionTo(CopyStatus to, params CopyStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            throw new DomainRuleViolationException(
                $"A copy cannot go from {Status} to {to}.");
        }

        Status = to;
    }
```

and the same substitution in `Regrade`, `RaiseGradeAfterRepair` and `CatalogSet.Catalogue`.

### What stayed an `ArgumentException`, and why that line matters

`Copy.Register` still calls `ArgumentNullException.ThrowIfNull(label)`. `CatalogSet.Catalogue` still
calls `ThrowIfNegative(retailPrice.Amount)`. `GradeMultipliers` still throws `ArgumentException`.
**None of those becomes the new type:**

> A `DomainRuleViolationException` means *you asked for something this business does not allow*.
> An `ArgumentException` means *the calling code is wrong*.

A null label is not a staff member making a decision the shop refuses — it is a programmer passing
null, and no message helps the person at the counter because the person at the counter did not cause
it. Episode 22's validator is what stops any of those reaching the domain from an HTTP request,
which is why they can stay a 500: **a request that gets past the validator and then trips an
argument guard is, by definition, a bug in this codebase.**

---

## Step 3 — Green: mapping it to a status code

`src/Catalog/BrickShare.Catalog.Api/DomainRuleViolationExceptionHandler.cs`:

```csharp
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Diagnostics;

namespace BrickShare.Catalog.Api;

/// <summary>
/// Turns a refused business rule into a 409 the client can read. Anything else is left alone,
/// so a genuine defect is still reported as the 500 it is.
/// </summary>
public sealed class DomainRuleViolationExceptionHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainRuleViolationException violation)
        {
            // False means "not mine". The next handler gets it, and if nobody claims it the
            // pipeline produces episode 22's 500 — the correct answer for a bug.
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = violation,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The catalog refused this change.",

                // Safe to put on the wire because the type says so. This message was written
                // for a person; an InvalidOperationException's was not.
                Detail = violation.Message
            }
        });
    }
}
```

registered in `Program.cs` above `AddProblemDetails`:

```csharp
builder.Services.AddExceptionHandler<DomainRuleViolationExceptionHandler>();
```

```
HTTP/1.1 409 Conflict

{
  "title": "The catalog refused this change.",
  "status": 409,
  "detail": "A set cannot require 30 days. The shop rents for at most 28 days, so a longer minimum could never be met.",
  "instance": "/api/v1/catalog/sets",
  "traceId": "00-8a3f…-01"
}
```

**The sentence in `detail` was written in episode 20, in the domain, by somebody thinking about LEGO
rentals.** It has travelled from a domain rule to a staff member's screen without anybody writing a
second version of it for the API layer, and without the API layer knowing what a rental is. That is
what the type bought.

### Why 409, and the honest case for 422

This is a genuine judgement call and the course should not pretend otherwise.

`docs/architecture/catalog.md` says 409, and 409 is what ships. The reasoning: **409 Conflict is the
only 4xx in RFC 9110 whose definition is about the state of the system rather than the shape of the
request** — "a conflict with the current state of the target resource" describes both of this
block's refusals, this one and episode 24's duplicate set number. One status code, one handler, one
thing for a client to learn.

**422 Unprocessable Content is defensible and arguably more precise**: the request is syntactically
fine and semantically unacceptable, which is exactly what happened. Two reasons 409 wins here,
neither overwhelming — splitting them means deciding *per rule* whether a refusal is about state or
about semantics, which different developers answer differently on different Tuesdays; and the client
behaviour is identical either way, so the distinction changes no behaviour and belongs in `detail`.

If your team has standardised on 422, use 422; nothing else changes. **What would be wrong is 400**,
which tells a client its request was malformed and sends a developer hunting a typo that is not
there.

---

## What this episode is not

**Not every refusal yet.** Catalogue the same set twice and the answer is still a 500, from a
different direction — the database, not the domain. Episode 24.

**No logging of refusals.** *How often does staff try to catalogue a set twice* is a question worth
answering and it is episode 34's, along with the rule about what must never be logged.

**No change to `ArgumentException` handling**, deliberately, per step 2. Those stay 500s and should.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings — including CA1032 on the new exception type |
| `dotnet test` | Green, including the twelve refactored assertions |
| `POST` with `minimumRentalDays: 30` | `409`, `detail` quoting the 28-day rule verbatim |
| `POST` with `{}` | Still `400`. Episode 22's path is untouched |
| A deliberately thrown `InvalidOperationException` in the handler | Still `500` — the handler returns `false` for anything that is not a domain rule violation |
| `grep -rn "InvalidOperationException" src tests` | Only in `Program.cs`'s datasource guard from episode 18 |

And one deliberate breakage, in the style episode 15 ended with. In
`DomainRuleViolationExceptionHandler`, change the type check:

```csharp
        if (exception is not InvalidOperationException violation)
```

Everything still compiles — and **the 409 test goes red**, because `DomainRuleViolationException`
does not derive from `InvalidOperationException`, so the handler now claims nothing. That is step
2's inheritance decision defending itself: had the type derived from `InvalidOperationException` to
be "compatible", this edit would have been silent, and the service would have started reporting
disposed-`DbContext` bugs to staff as *the catalog refused this change*.

## Next

[Episode 24 — Two people, one Titanic](episode-24.md):
the shop owns three Titanics — that is **one** catalog entry and three copies, and nothing yet stops
a second person creating a second entry.

Episode 24 closes the last of the three gates, and it is the one that cannot be closed in C#: the
obvious fix is to query before inserting, and the obvious fix is a race. **The database is the only
participant that sees both transactions.**
