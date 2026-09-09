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
`application/problem+json` and in the client's own spelling, and the episode has drawn the line it
exists to draw: **the edge rejects nonsense; it does not replace the domain.**

## Before recording

- Episode 21 merged: the endpoint live, `201` from Azure.
- `docker compose up`, and a branch.
- [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) open in a tab. Two minutes scrolling it beats
  any description of it.
- [`episode-21.md`](episode-21.md) open at its `Asp.Versioning` argument — step 2 makes an exception
  to it and should be able to quote what it is excepting.

**This episode is red-green throughout, with one exemption.** A validator is behaviour with inputs
and outputs and nothing else; if any code in this course deserves a failing test first, it is this —
and step 2 gets *two* reds from the same test, the second of which is the library disagreeing with
the wire format. The exemption is the `MaxLength` visibility change, which `CLAUDE.md` covers: a
demonstration whose whole point is that it compiles.

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

### The package, and the rule it is an exception to

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="FluentValidation" Version="12.1.1" />
```

```xml
<!-- BrickShare.Catalog.Api.csproj -->
<PackageReference Include="FluentValidation" />
```

**Stop and justify this**, because a student who watched episode 21 is entitled to object: that
episode refused `Asp.Versioning` on the rule this course keeps applying — *libraries appear when
something forces them* — and here is one arriving for a single validator.

Two reasons this is an exception rather than the rule quietly bending. **`Asp.Versioning` solves a
problem this service does not have** — two live versions — while FluentValidation solves one that is
on screen now, and has two more scheduled: the lookup in episode 26 and batch registration in
episode 27, where `RuleForEach` is what a pile of `if` statements handles worst. And **it is standard
equipment** — a course that never shows it leaves a gap that hand-rolled `if`s do not fill.

One package, not two: `FluentValidation.DependencyInjectionExtensions` only scans assemblies, and
step 2 registers by hand.

### The validator

`Endpoints/CatalogueSetRequestValidator.cs`:

```csharp
using BrickShare.Catalog.Domain;

using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class CatalogueSetRequestValidator : AbstractValidator<CatalogueSetRequest>
{
    public CatalogueSetRequestValidator()
    {
        // Delegated: the domain owns what a set number is. This asks it. See below.
        RuleFor(request => request.SetNumber)
            .Must(value => SetNumber.TryParse(value, out _))
            .WithMessage("A set number is required, and cannot be longer than 32 characters.");

        RuleFor(request => request.Name).NotEmpty()
            .WithMessage("A name is required.");

        RuleFor(request => request.Theme).NotEmpty()
            .WithMessage("A theme is required.");

        // LEGO's first plastic brick shipped in 1949. Anything earlier is a typo.
        RuleFor(request => request.Year).GreaterThanOrEqualTo(1949)
            .WithMessage("A year is required, and LEGO has not existed since before 1949.");

        RuleFor(request => request.PieceCount).GreaterThanOrEqualTo(1)
            .WithMessage("A set has at least one piece.");

        RuleFor(request => request.RetailPrice).GreaterThanOrEqualTo(0m)
            .WithMessage("A retail price cannot be negative.");

        RuleFor(request => request.BaseRentalPrice).GreaterThanOrEqualTo(0m)
            .WithMessage("A base rental price cannot be negative.");

        RuleFor(request => request.MinimumRentalDays).GreaterThanOrEqualTo(1)
            .WithMessage("A rental lasts at least one day.");

        RuleFor(request => request.MinimumAge).InclusiveBetween(0, 18)
            .WithMessage("An age rating is between 0 and 18.");
    }
}
```

Registered by hand, in `Program.cs`:

```csharp
builder.Services.AddScoped<IValidator<CatalogueSetRequest>, CatalogueSetRequestValidator>();
```

**Not `AddValidatorsFromAssemblyContaining<Program>()`**, the line every tutorial uses. With one
validator, scanning replaces a line you can read with a line you have to trust, and `CLAUDE.md` is
explicit about preferring the first. Scanning is right at three validators; episode 27 swaps it in.

### Calling it

```csharp
    private static async Task<Results<Created<CatalogSetResponse>, ValidationProblem>> CatalogueAsync(
        CatalogueSetRequest request,
        IValidator<CatalogueSetRequest> validator,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            // ToDictionary() produces exactly the shape ValidationProblem wants. This is the
            // whole integration, and it is the reason the library costs nothing to adopt here.
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }
        …
```

**The endpoint filter is the idiomatic setup and is deliberately not used.** A filter means the
handler never mentions validation and the check happens somewhere the reader has to go looking for —
better engineering, worse teaching in the episode that is about to draw three gates and point at one
of them. **A gate you cannot see in the handler is a gate you take on trust.** It is the right
refactor once every endpoint validates, and one line then.

### Still red — the library has an opinion about your field names

```
Assert.Contains() Failure: Expected "setNumber", found "SetNumber"
```

`ToDictionary()` keys on the **C# property name** — `SetNumber`, `PieceCount`, `MinimumRentalDays`,
none of which the client sent. **A validation error naming a property nobody typed is worse than no
message at all**, and a hand-rolled validator writes the client's spelling as a literal and cannot
get this wrong. This is the first thing the library costs. In `Program.cs`:

```csharp
// FluentValidation names errors after C# properties. The wire is camelCase, so the two have to be
// reconciled somewhere, and this is the only place FluentValidation offers.
ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
    member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);
```

with `using System.Text.Json;` and `using FluentValidation;`.

**Say plainly what that is: a mutable global static.** Not a shape this course likes, and the only
alternative is `.OverridePropertyName("setNumber")` on all nine rules. **It is the price of the
library, paid once, where startup decisions are visible** — and a dependency that appears to cost
nothing usually means you have not looked yet.

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

**Every field at once, not the first one.** A validator that stopped at the first failure would make
a staff member with six empty fields submit the form six times. FluentValidation collects them by
default — worth naming as something it gives away that hand-rolled validators routinely get wrong,
because `return` is such a natural thing to type inside an `if`.

### The one rule that does not duplicate anything

Two of those nine rules are not the same kind of thing, and the episode falls apart if they look
like it:

```csharp
.Must(value => SetNumber.TryParse(value, out _))          // asks the domain
.GreaterThanOrEqualTo(1)   // on MinimumRentalDays        // re-states a rule the domain also enforces
```

The first has **one** implementation, in `SetNumber`. The second has two — this rule, and
`ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1)` inside `CatalogSet.Catalogue`.

The format of a set number **is** a domain rule. That is precisely why the edge calls it instead of
reimplementing it, and it is the sentence to leave on screen:

> The edge is not validating the set number. It is **asking the domain to**, and translating the
> answer into HTTP.

Episode 13 built the `Parse` / `TryParse` pair for this exact moment and said so: *"episode 22's
edge validation calls `TryParse` and turns `false` into a `ProblemDetails` with a good message."*
**Read that line back on camera** — a seam designed nine episodes early and used without
modification is the best evidence this course has that designing before building is not ceremony.

Without it, `SetNumber.Parse(null)` throws `FormatException`, which is not a domain rule violation,
so episode 23's handler passes it through as a **500**.

### The `32` in that message is a second source of truth

Look again at the message the first rule produces:

```csharp
.WithMessage("A set number is required, and cannot be longer than 32 characters.");
```

`MaxLength` is a `private const int MaxLength = 32` inside `SetNumber`. Change it to 24 and **that
sentence silently starts lying, with no test to catch it** — step 1 asserts the *key* `setNumber` and
never looks at the text.

One word in a file from episode 13:

```csharp
public const int MaxLength = 32;        // was private
```

and the message interpolates it:

```csharp
            .WithMessage(
                $"A set number is required, and cannot be longer than {SetNumber.MaxLength} characters.");
```

Change the const to 24, re-run the `curl`, watch the response follow with no second edit, and put it
back. **Not test-driven** — `CLAUDE.md` names the exemption: a demonstration whose whole point is
that it compiles.

**The trade-off, stated:** this widens a domain type's public surface so an error message can quote
it. The alternative is a vaguer sentence that can never go stale and helps nobody. `LabelCode` has
the identical shape and wants the same treatment when episode 27 validates a label.

---

## Step 3 — `ProblemDetails`, and the line this block is really about

### The shape is somebody else's decision, on purpose

That response is **RFC 9457**, and the point is that the shape is not ours — `application/problem+json`
lets a client tell an error body from a success body **without parsing it first**, and every framework
in every language already reads it. `TypedResults.ValidationProblem` produces it for free.

The alternative is an in-house envelope whose failure mode is not that it is bad but that it is
*inconsistent*: twelve endpoints, four developers, and a client needing a function that tries
`{"error": …}`, `{"message": …}` and a bare string. **A standard is worth using mostly because it
stops being a decision.** Making it the house format for failures nobody wrote a `return` for is
episode 23 — that needs an unhandled exception to be worth watching.

### Edge validation does not replace the domain

Now the idea the episode exists to land. The instinct is to sort rules into two buckets — edge rules
and domain rules — and that is the wrong shape. **The question is not where a rule lives. It is who
decides it and who answers the caller**, and the nine rules in this validator split three ways:

| Kind | Example | Implementations |
| --- | --- | --- |
| **Delegated** | `.Must(v => SetNumber.TryParse(v, out _))` | **One**, the domain's. The edge asks and turns the answer into a 400 |
| **Duplicated on purpose** | `.GreaterThanOrEqualTo(1)` on days | **Two**, accepted. The domain's throws `ArgumentOutOfRangeException` — a 500 by design — so the edge restates it to name the field |
| **Domain only** | the 28-day maximum | **One**, and not here |

**The bottom row is the whole argument.** Zero days is nonsense in any rental business that ever
existed, so the edge names the field: *could any shop, anywhere, mean this?* Twenty-nine is perfectly
sensible — plenty of shops rent by the month — and episode 20 made it illegal because of a Stripe
authorization window in a service this one has never heard of: *does **this** shop allow it?* Two
copies of that policy, in two files, with nothing linking them, is the bug.

The middle row is worth defending rather than apologising for: the domain check exists so **a batch
importer, a seeder or next year's message handler cannot construct an illegal `CatalogSet`** by going
around an endpoint nobody remembered to reuse.

### And the library just made the wrong thing easier

Point at the `RuleFor` chain while saying this, because it is a cost episode 21's `if` statements did
not have. `[Required]` **cannot** express the 28-day rule. FluentValidation expresses it in six
characters:

```csharp
        RuleFor(request => request.MinimumRentalDays).LessThanOrEqualTo(28);   // do not
```

That line would pass every test in this episode, read perfectly well in review, and quietly put a
business policy in the wrong file. **The temptation this step warns against just became one
autocomplete**, and that is the honest trade for the thirty lines the library saved.

The sentence to leave on screen:

> **Doing only edge validation gives you an API that is safe until something calls the code another
> way — and something always calls the code another way.**

---

## What this episode is not

**No DataAnnotations.** `[Required]` and `[Range]` cannot express the delegated rule at the top of
this validator without a custom attribute — and the moment you write one, you have written a worse
`IValidator<T>`.

**No endpoint filter, no assembly scanning, no `FluentValidation.TestHelper`.** All three are right
refactors waiting for episode 27, where three validators make them earn their place.

**No async or DI rules.** FluentValidation can query a database inside a rule — *"is this already
catalogued?"* — and episode 24 is about why that check must not live there. A capability worth
knowing, and a trap.

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
| Those seven keys | **camelCase** — `setNumber`, not `SetNumber`. Delete the `PropertyNameResolver` line and watch all three key assertions fail |
| `SetNumber.MaxLength` changed to 24 | The 400 message says *24 characters* with no second edit. Put it back |
| `POST` with a valid body | Still `201`. The validator refuses nothing it should not |
| `POST` with `minimumRentalDays: 30` | **`500` with an empty body.** Expected — episode 23 |
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
