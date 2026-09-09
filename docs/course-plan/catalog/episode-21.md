# Episode 21 — The first endpoint

← [Course plan](catalog-api.md) · Previous: [Episode 20 — The set the copies are copies of](episode-20.md)

Episode 20 built a `CatalogSet` and a table to keep it in. Nothing can create one except a test.

This episode opens the door — and the door is worth an episode because almost every decision in it
is one you make once and live with for the life of the API. Where endpoints are declared, what the
URL looks like, and what comes back on success are all cheap today and expensive the moment somebody
is depending on them.

**Done when** `POST /api/v1/catalog/sets` returns `201 Created` from Azure, with a row in
`catalog_sets` to show for it.

## Before recording

- Episode 20 merged: `CatalogSet`, `CatalogSetConfiguration`, the `AddCatalogSets` migration applied
  in Azure.
- `docker compose up` working — most of this episode is `curl` against localhost.
- A branch.
- [`docs/architecture/catalog.md`](../../architecture/catalog.md) open at *API surface*.

**Step 1 is wiring and says so; steps 2 and 3 are red-green.** A `MapGroup` call has no behaviour to
drive out — `CLAUDE.md` exempts exactly this. The handler does, and it starts from a test that gets
a 404.

Keep the architecture document's *API surface* table on screen while step 1 runs. This endpoint is
**one row of eleven**, and the route organisation only makes sense against the other ten.

---

## Step 1 — `/api/v1`, and a group with one endpoint in it

`Program.cs` currently maps three things by hand — `/`, `/health/live`, `/health/ready`. Adding a
fourth line there would work today and be the wrong habit by episode 30, so the shape goes in first.

`src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs`:

```csharp
namespace BrickShare.Catalog.Api.Endpoints;

public static class CatalogSetEndpoints
{
    public static RouteGroupBuilder MapCatalogSets(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog/sets")
            .WithTags("Catalog sets");

        return group;
    }
}
```

and in `Program.cs`, after the health checks:

```csharp
// Everything a client calls lives under a version. See below: this is a prefix, not a library.
RouteGroupBuilder v1 = app.MapGroup("/api/v1");

v1.MapCatalogSets();
```

### Why the endpoints are not in `Program.cs`

They could be, and for one endpoint it would read better. The reason not to is that `Program.cs` is
the **composition root** — the file you open to find out what this application is made of — and it
stays useful exactly as long as it stays a list. Six episodes from now there are eleven endpoints
across three resources, and a `Program.cs` holding all of them is a file nobody reads, only searches.

`MapCatalogSets()` is one line in that list and the grep target for everything about sets. **The
group is the seam, and a static extension method is the cheapest way to have one** — no controller,
no base class, no convention to learn.

### Why `/api/v1` is a string and not `Asp.Versioning`

`Asp.Versioning.Http` is a good library. It does header and query-string versioning, deprecation
headers, version sets, and API-version-aware OpenAPI documents. **Every one of those features is for
running two versions at once**, and this service runs zero.

So the prefix goes in as a literal, because the thing worth having today is not the machinery — it
is the *URL shape*. Versioning the first endpoint costs six characters. Versioning the fortieth
endpoint costs a migration plan, a deprecation window and a conversation with every consumer, and
that is the version of this decision that actually hurts. **The prefix is free now and unbuyable
later**, which is the only real reason to do anything early.

When there genuinely is a v2 — a breaking change to a shape a customer depends on — the library goes
in, and it goes in *knowing what it has to support* rather than guessing. Same argument episode 2
made about projects and episode 7 made about Terraform modules: structure appears when something
forces it.

**This step is wiring**, and no test would say anything the two lines do not.

---

## Step 2 — Red: a request nobody can serve

`tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;

namespace BrickShare.Catalog.IntegrationTests.CatalogSets;

public class CatalogueSetTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_catalogued_set_comes_back_created()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static object ValidRequest() => new
    {
        setNumber = "10294-1",
        name = "Titanic",
        theme = "Icons",
        year = 2021,
        pieceCount = 9090,
        retailPrice = 629.99m,
        baseRentalPrice = 60.00m,
        minimumRentalDays = 7,
        minimumAge = 18
    };
}
```

```
Assert.Equal() Failure: Values differ
Expected: Created
Actual:   NotFound
```

An honest red, and it costs nothing to point out **which infrastructure this test did not have to
build**: it inherits `DatabaseTest`, so a real Postgres is already running, already migrated,
already reset before this method started. That is episode 17's fixture being used by an episode that
had not been written when it was designed, which is a fair test of whether a fixture was designed
well.

---

## Step 3 — Green: the handler

Three pieces: what a client may send, what it gets back, and the eight lines between them.

```csharp
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http.HttpResults;

namespace BrickShare.Catalog.Api.Endpoints;

public static class CatalogSetEndpoints
{
    public static RouteGroupBuilder MapCatalogSets(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog/sets")
            .WithTags("Catalog sets");

        group.MapPost("/", CatalogueAsync);

        return group;
    }

    private static async Task<Created<CatalogSetResponse>> CatalogueAsync(
        CatalogueSetRequest request,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        CatalogSet set = CatalogSet.Catalogue(
            SetNumber.Parse(request.SetNumber),
            request.Name,
            request.Theme,
            request.Year,
            request.PieceCount,
            new Money(request.RetailPrice),
            new Money(request.BaseRentalPrice),
            request.MinimumRentalDays,
            request.MinimumAge);

        database.Sets.Add(set);
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/v1/sets/{set.Id}", CatalogSetResponse.From(set));
    }
}
```

and the two contracts, in the same file because they exist only for this endpoint:

```csharp
/// <summary>
/// What staff send to catalogue a set. Every product fact in here is client-supplied, which is
/// a security problem episode 26 exists to fix.
/// </summary>
public sealed record CatalogueSetRequest(
    string SetNumber,
    string Name,
    string Theme,
    int Year,
    int PieceCount,
    decimal RetailPrice,
    decimal BaseRentalPrice,
    int MinimumRentalDays,
    int MinimumAge);

public sealed record CatalogSetResponse(
    Guid Id,
    string SetNumber,
    string Name,
    string Theme,
    int Year,
    int PieceCount,
    decimal RetailPrice,
    decimal BaseRentalPrice,
    int MinimumRentalDays,
    int MinimumAge)
{
    public static CatalogSetResponse From(CatalogSet set) => new(
        set.Id,
        set.Number.Value,
        set.Name,
        set.Theme,
        set.Year,
        set.PieceCount,
        set.RetailPrice.Amount,
        set.BaseRentalPrice.Amount,
        set.MinimumRentalDays,
        set.MinimumAge);
}
```

```bash
dotnet test    # green
```

### Why the response is a record and not the entity

`CatalogSetResponse` is nine properties that are also on `CatalogSet`, and the first instinct is to
delete it and serialize the domain object. Two reasons not to.

The visible one: `SetNumber` and `Money` are the value objects episode 13 fought for, and
`System.Text.Json` renders them as `{"value":"10294-1"}` and `{"amount":629.99}`. Every consumer
would carry a wrapper object for a wrapper object, forever, because of a decision internal to this
service's domain model.

The real one: **the wire format is a contract with people, and the domain model is a tool for
solving problems.** They change for unrelated reasons. Renaming `CatalogSet.Theme` to `ThemeName`
should be a refactor; if the entity is the response, it is a breaking API change and somebody's
client stops working.

**And the counter-argument, honestly:** at this size the two are identical and the mapping is pure
overhead. The reason to pay it is that the divergence arrives on a date this course can name —
episode 30 adds available copy count and a starting price to the read model, numbers that are
computed, live on no entity, and belong on the wire.

### `Created`, and a `Location` that does not work yet

`TypedResults.Created($"/api/v1/sets/{set.Id}", …)` sets a `Location` header pointing at
`GET /api/v1/sets/{id}` — **and that route does not exist until episode 30**, so following it right
now returns 404.

That is a real wart, chosen over the alternatives on purpose. Returning `Created` without a
`Location` removes the thing that makes 201 more useful than 200, and the day the read endpoint
lands nobody remembers to add it. **A dangling reference you have named is a to-do; one you have not
is a bug**, and the verification table below lists it as an expected 404 rather than pretending.

### One thing about that request record that is not true

`string SetNumber` is non-nullable and **the deserializer will happily put `null` in it** — nullable
reference types protect nothing at a deserialization boundary. Leaving it non-nullable is a claim
that episode 22 makes true. **A DTO's nullability describes the state after validation, not after
binding**, and knowing which you are looking at is the difference between a necessary null check and
superstition.

---

## Step 4 — Live in Azure, and the `curl` nobody enjoys

```bash
git push
```

```
✓ build-test   ✓ image   ✓ migrate   ✓ deploy
```

```bash
curl -i -X POST "$(cd infra && terraform output -raw web_app_url)/api/v1/catalog/sets" \
  -H 'Content-Type: application/json' \
  -d '{ "setNumber": "10294-1", "name": "Titanic", "theme": "Icons", "year": 2021,
        "pieceCount": 9090, "retailPrice": 629.99, "baseRentalPrice": 60.00,
        "minimumRentalDays": 7, "minimumAge": 18 }'
```

```
HTTP/1.1 201 Created
location: /api/v1/sets/019a3f7c-…
```

**Sit on this.** A row a customer will eventually browse now exists in a managed Postgres in Azure,
written by an application that authenticated with no password, into a table created by a pipeline
step, through an endpoint deployed by a workflow nobody had to babysit.

And then the sobering half, in the same breath:

```bash
# From anywhere on the internet. No token. No header. No account.
curl -X POST "https://app-brickshare-catalog-dev.azurewebsites.net/api/v1/catalog/sets" …
```

**That works, and it should not.** Nothing in this service knows what a staff member is. Episode 33
is the fix, it is twelve episodes away, and the reason it is not today is that authorization on one
endpoint teaches almost nothing — the interesting version of that episode needs a surface with roles
that genuinely differ. Until then this is a `dev` resource group with a `dev` database and a URL
nobody has been given, which is a mitigation and not a control.

---

## What this episode is not

**No validation.** Send `{}` and the answer is a 500. That is not an oversight — it is episode 22,
and it is worth leaving broken for one episode so the fix has something to fix.

**No error handling of any kind.** A refused business rule, a duplicate set number and a genuine bug
all currently produce the same empty 500. Episodes 22, 23 and 24 take those one at a time.

**No authorization**, as step 4 said out loud.

**No OpenAPI document.** The endpoint exists and nothing describes it. That waits until episode 28,
when there are three endpoint groups and a document is a document rather than a list of one.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green, including `A_catalogued_set_comes_back_created` |
| `POST /api/v1/catalog/sets`, valid body | `201`, `Location: /api/v1/sets/{id}`, a row in `catalog_sets` |
| Following that `Location` | **`404` until episode 30.** Expected, and the reason is in step 3 |
| `POST` with `{}` | `500` with an empty body. **Expected today** — episode 22 |
| `GET /health/live`, `/health/ready` | Still 200. The new group changed no existing route |
| The same `POST` against the Azure URL | `201`, no terminal touched to get there |

## Next

[Episode 22 — Refusing nonsense at the edge](episode-22.md):
the endpoint accepts anything, and answers `500 Internal Server Error` when it cannot cope.

The server did not fail — the request was nonsense, and nobody can act on a 500: not the staff
member who typed it, not the developer reading the logs, not the on-call engineer whose error-rate
dashboard just moved. Episode 22 adds validation at the edge and `ProblemDetails` (RFC 9457) as the
house error format, and then draws the line this whole block is really about: **the edge rejects
nonsense; it does not replace the domain.**
