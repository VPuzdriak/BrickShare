# Episode 22 — Refusing nonsense at the edge

← [Course plan](catalog-api.md) · Previous: [Episode 21 — The first endpoint](episode-21.md)

The endpoint from episode 21 works, and the green test suite is hiding something:

```bash
curl -i -X POST http://localhost:5080/api/v1/catalog/sets \
  -H 'Content-Type: application/json' \
  -d '{ "name": "", "theme": "", "year": 0, "pieceCount": 0,
        "retailPrice": -5, "baseRentalPrice": 0, "minimumRentalDays": 0, "minimumAge": 0 }'
```

```
HTTP/1.1 500 Internal Server Error
content-length: 0
```

`SetNumber.Parse(null)` throws, or `ThrowIfNullOrWhiteSpace` does, or `ThrowIfNegative` does — and
whichever wins, the client is told the server broke. **The server did not break. The request was
nonsense.** Nobody can act on a 500: not the staff member who typed it, not the developer reading
the logs, not the on-call engineer whose error-rate dashboard just moved.

**Done when** a malformed request comes back `400` with every failing field named at once, in
`application/problem+json`, and the episode has drawn the line it exists to draw: **the edge rejects
nonsense; it does not replace the domain.**

## Before recording

- Episode 21 merged: the endpoint live, `201` from Azure.
- `docker compose up`.
- A branch.
- The RFC open in a tab — [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457), *Problem Details for
  HTTP APIs*. Two minutes of scrolling it is worth more than any description of it.

**This episode is red-green throughout.** A validator is behaviour with inputs and outputs and
nothing else; if any code in this course deserves a failing test first, it is this.

---

## Step 1 — Red

```csharp
    [Fact]
    public async Task An_empty_request_is_refused_field_by_field()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("setNumber", problem.Errors.Keys);
        Assert.Contains("name", problem.Errors.Keys);
        Assert.Contains("minimumRentalDays", problem.Errors.Keys);
    }
```

with `using Microsoft.AspNetCore.Http;` for `HttpValidationProblemDetails`.

```
Expected: BadRequest
Actual:   InternalServerError
```

---

## Step 2 — Green: a validator

Alongside the request record:

```csharp
internal static class CatalogueSetRequestValidation
{
    public static bool IsValid(
        this CatalogueSetRequest request,
        out Dictionary<string, string[]> errors)
    {
        // The keys are the names the CLIENT sent, not the C# property names. A validation error
        // naming a property nobody typed is a worse message than no message at all.
        Dictionary<string, string[]> found = [];

        if (!SetNumber.TryParse(request.SetNumber, out _))
        {
            found["setNumber"] = ["A set number is required, and cannot be longer than 32 characters."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            found["name"] = ["A name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Theme))
        {
            found["theme"] = ["A theme is required."];
        }

        if (request.Year < 1949)
        {
            // LEGO's first plastic brick shipped in 1949. Anything earlier is a typo.
            found["year"] = ["A year is required, and LEGO has not existed since before 1949."];
        }

        if (request.PieceCount < 1)
        {
            found["pieceCount"] = ["A set has at least one piece."];
        }

        if (request.RetailPrice < 0m)
        {
            found["retailPrice"] = ["A retail price cannot be negative."];
        }

        if (request.BaseRentalPrice < 0m)
        {
            found["baseRentalPrice"] = ["A base rental price cannot be negative."];
        }

        if (request.MinimumRentalDays < 1)
        {
            found["minimumRentalDays"] = ["A rental lasts at least one day."];
        }

        if (request.MinimumAge is < 0 or > 18)
        {
            found["minimumAge"] = ["An age rating is between 0 and 18."];
        }

        errors = found;
        return found.Count == 0;
    }
}
```

and two lines at the top of the handler, plus a widened return type:

```csharp
    private static async Task<Results<Created<CatalogSetResponse>, ValidationProblem>> CatalogueAsync(
        CatalogueSetRequest request,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!request.IsValid(out Dictionary<string, string[]> errors))
        {
            return TypedResults.ValidationProblem(errors);
        }
        …
```

Green, and the `curl` from the cold open now says something useful:

```
HTTP/1.1 400 Bad Request
content-type: application/problem+json

{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "setNumber": ["A set number is required, and cannot be longer than 32 characters."],
    "name": ["A name is required."],
    "theme": ["A theme is required."],
    "year": ["A year is required, and LEGO has not existed since before 1949."],
    "pieceCount": ["A set has at least one piece."],
    "retailPrice": ["A retail price cannot be negative."],
    "minimumRentalDays": ["A rental lasts at least one day."]
  }
}
```

**Every field at once, not the first one.** A validator that returns on the first failure makes a
staff member with six empty fields submit the form six times. Collecting all of them is four extra
characters of code and the entire difference between a usable form and an infuriating one.

### Why thirty lines of `if` and not `[Required]`

DataAnnotations would be shorter. It is not used here for two reasons, and the second is the one
that matters.

The messages are written for the person reading them — *"A set has at least one piece"* is not a
sentence an attribute generates. And **the rule that is deliberately absent from this validator is
the point of the next episode**: an attribute-driven validator makes putting every rule at the edge
the path of least resistance, which is exactly the habit step 3 is about to argue against.

---

## Step 3 — `ProblemDetails`, and the line this block is really about

### The shape is somebody else's decision, on purpose

That response is **RFC 9457**, and the point of using it is that it is not ours: `type`, `title`,
`status`, `detail`, `instance`, plus an `errors` object for the validation case, and
`application/problem+json` so a client can tell an error body from a success body **without parsing
it first**.

The alternative is an in-house error envelope, and its failure mode is not that it is bad — it is
that it is *inconsistent*. Twelve endpoints written over two years by four people produce
`{"error": …}`, `{"message": …}`, `{"errors": […]}` and a bare string, and every client ends up with
a function that tries all four. **A standard is worth using mostly because it stops being a
decision**, and decisions re-made per endpoint get made differently per endpoint.

So make it the house format for *every* failure, not just this one. `Program.cs`:

```csharp
// Turns any unhandled failure into RFC 9457 instead of an empty body.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;

        // One id that appears in the response and in the logs, so a screenshot from a staff
        // member is enough to find the request. Episode 34 wires the other end of this.
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    });
```

and, right after `var app = builder.Build();`:

```csharp
app.UseExceptionHandler();
```

with `using System.Diagnostics;` at the top.

### Edge validation does not replace the domain

Now the idea the episode exists to land, and it is genuinely easy to get backwards.

| | Edge validation | Domain rules |
| --- | --- | --- |
| Answers | *Could any shop, anywhere, mean this?* | *Does **this** shop allow it?* |
| Runs | Once, per HTTP request | Every time the object is created, by anyone |
| Rejects | Nonsense, typos, missing fields | Legal-looking requests the business refuses |
| Speaks to | A person filling in a form | A programmer — and, from episode 23, a person |
| Changes when | The wire format changes | The business changes |
| Example | `minimumRentalDays` is 0 | `minimumRentalDays` is 29 |

**The example row is the whole argument.** Zero days is nonsense in any rental business that has
ever existed, so the edge catches it and names the field. Twenty-nine days is a perfectly sensible
number — plenty of shops rent by the month — and episode 20 made it illegal because of a rule about
a Stripe authorization window in a service this one has never heard of. That rule is **not** in the
validator, and putting it there would be the actual mistake: two copies of a business policy that
will change, in two files, with nothing linking them.

Notice the deliberate overlap, too. `minimumRentalDays < 1` is checked in *both* places, and that is
not an oversight to clean up. The edge check exists so an HTTP caller gets the field named. The
domain check exists so that **a future batch importer, a seeder, a migration script or next year's
message handler cannot construct an illegal `CatalogSet`** by going around an endpoint nobody
remembered to reuse.

The sentence to leave on screen:

> **Doing only edge validation gives you an API that is safe until something calls the code another
> way — and something always calls the code another way.**

---

## What this episode is not

**Not FluentValidation, not DataAnnotations, not `AddValidation`.** Each would work. Step 2 said why
none of them is here, and the reason is about what they make easy rather than what they cost.

**Not the end of the 500s.** Send a *well-formed* request the business refuses and the answer is
still a 500 — now with a tidy RFC 9457 body claiming the server failed. That is episode 23, and it
is a better episode for having this one's table already on the wall.

**No `traceId` in the logs yet.** The extension is on the response and nothing correlates it to a
log line. Episode 34.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green, including `An_empty_request_is_refused_field_by_field` |
| `POST` with `{}` | `400`, `application/problem+json`, seven fields in `errors` |
| `POST` with a valid body | Still `201`. The validator refuses nothing it should not |
| `POST` with `minimumRentalDays: 30` | **`500`, now with a ProblemDetails body.** Expected — episode 23 |
| Every 4xx and 5xx response | Carries `instance` and `traceId` |
| The Azure URL, same three requests | Identical answers |

## Next

[Episode 23 — A refused rule is not a bug](episode-23.md):
one request in this episode's verification table still returns 500, and it is a request no staff
member would think twice about typing.

The domain refuses it correctly, with a message written for a human — and that message reaches
nobody, because episodes 14, 15 and 20 all refuse business rules by throwing
`InvalidOperationException`: the same exception the runtime throws for a disposed `DbContext`, a
double-awaited `Task` and a collection modified during iteration. **An API layer cannot tell a
business rule from a bug**, and episode 23 fixes that with a type.
