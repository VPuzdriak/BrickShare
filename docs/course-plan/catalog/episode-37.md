# Episode 37 — Set detail, and the rules that are easy to break

← [Course plan](catalog-api.md) · Previous: [Episode 36 — Search, and page two](episode-36.md)

Episode 36 finished the listing. A customer can find the Titanic, see that one copy is available,
and see that it starts at 51.00. They can't see **which** copy, why it costs 51.00, or what the
other boxes look like. That is the next screen in UC-7:

> **UC-7.3 — View a set.** Product facts (name, year, theme, piece count, image, age rating),
> the minimum rental duration, and **every copy the shop owns** — not only the free ones.
>
> **UC-7.4 — Compare the copies.** For each copy: its **condition grade**, how **complete** it
> is, **photographs** of its actual state, its **rental price** and its **deposit**.
>
> **UC-7.5 — Act on a copy.** Available copies can be **reserved**. Any copy, available or not,
> can be **subscribed to**.

This episode builds `GET /catalog/sets/{id}`. It sounds like the easiest endpoint in the service: one
set, its copies, done. It isn't. **UC-7 has rules that a query written from intuition gets wrong**,
and every step in the first half is one of those rules turned into a red test.

The second half is staff. Staff get `GET /catalog/copies/by-label/{code}`, the scan at the counter.
Two `Location` headers from episodes 21 and 30 also start pointing at routes that exist. One of them
turns out to have been wrong since episode 21.

**Done when:**
- `GET /catalog/sets/{id}` lists every copy the shop rents out, including copies on rent. Each copy
  has its grade, whether it is available, its rental price and its deposit, cheapest first.
- A set with no copies comes back in full with `startingPrice: null`, and a retired copy is not
  listed.
- Following the `Location` of a catalogued set, or of a delivery of copies, lands on that detail.
- Scanning a label finds the box in any status, and a mistyped label is a `400` that says why.

> **Runtime: about 22 minutes — over the 10–15 budget, and said so up front.** This episode has two
> subjects, which share a response type and nothing else. **Record it as two**, with the seam marked
> in place after step 6:
>
> - **37a — "Set detail" (steps 1–6, ~13 minutes).** The customer's view of one set, and the three
>   rules from UC-7 that decide what it shows.
> - **37b — "The scan, and headers that point somewhere" (steps 7–11, ~9 minutes).** The staff read of
>   a copy, and the `Location` headers episodes 21 and 30 left pointing at nothing.
>
> The numbering of episode 38 onwards does not move. The script reads correctly either way.

> **A change from the course plan, and why.** The plan puts a third rule here: *only published
> photographs are returned to customers*. There is no photograph in this service yet. Uploading
> is episode 38, and the published/evidence split is episode 39. Testing that rule now would mean
> building a `copy_photos` table that nothing can fill, just so a test has something to filter.
> That is the reasoning episodes 28–30 used about `Location` headers: *inventing a feature to make
> a string true*. So the rule is **named** in step 6, and **episode 39 adds `photos` to this
> response** together with its test.

## Before recording

- Episode 36 merged. `BrowseSetsTests` is green.
- `docker compose up`, with two or three sets catalogued and a few copies registered, so the
  `.http` requests in steps 6 and 11 have something to show. Keep one label code from a
  registration response in the clipboard for step 11.
- A branch.
- `docs/IDEA.md` open at UC-7.3–7.5 and its business rules, and `docs/architecture/catalog.md` open at
  *"The read-side rules that are easy to get wrong"*. Both are quoted on screen.

**Three tests pass as soon as they are written, and the script says so where they appear.** They
describe rules rather than drive the design. Step 6 and step 11 are mostly metadata and a request
collection, and the rename in step 9 is wiring. Everything else is red → green.

Every sample names its file and where in it the code goes. Where a file that already exists is
edited, the **first block is what is already there** (the anchor to find on screen) and the
**second block is what to paste**.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/IntegrationTests/Browse/SetDetailTests.cs` | **New** — five facts, two helpers |
| `tests/…/IntegrationTests/Copies/ScanLabelTests.cs` | **New** — five facts, two helpers |
| `tests/…/IntegrationTests/CatalogSets/CatalogueSetTests.cs` | One fact |
| `tests/…/IntegrationTests/Copies/RegisterCopiesTests.cs` | One fact |
| `src/…/Api/Endpoints/BrowseEndpoints.cs` | One route, its handler, two response records |
| `src/…/Api/Endpoints/CatalogSetEndpoints.cs` | One string: the `Location` |
| `src/…/Api/Endpoints/CopyEndpoints.cs` | One string, one route, its handler, one record, a rename |
| `src/…/Api/Program.cs` | One line: the rename |
| `src/…/Api/BrickShare.Catalog.Api.http` | Requests appended |

Nothing in `Domain/`, no migration and nothing in `infra/`. **Everything this endpoint needs
already exists.** The per-set numbers are in episode 34's view, the prices are in `PriceCalculator`
since episode 12, and the multipliers are in episode 34's seeded table. This episode is about
*reading* them correctly.

---

# 37a — Set detail

## Step 1 — Red → green: a set with no copies shows in full

The first rule is the one most likely to be "optimised" away:

> A set with no available copies still shows in full — otherwise its copies could not be
> subscribed to.

The strongest version of it is a set with **no copies at all**. `docs/IDEA.md` allows that: a
planned purchase, or a set whose every copy has been retired. That is the first test, and a new
file, because the listing tests are about many sets and these are about one:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — new file
using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.IntegrationTests.Browse;

public class SetDetailTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_set_with_no_copies_shows_in_full_with_no_price()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        Assert.Equal(setId, detail.Id);
        Assert.Equal("10294-1", detail.SetNumber);
        Assert.Equal("Titanic", detail.Name);
        Assert.Equal("Icons", detail.Theme);
        Assert.Equal(9092, detail.PieceCount);
        Assert.Equal(7, detail.MinimumRentalDays);
        Assert.Equal(0, detail.AvailableCount);

        // Not 0.00, for the reason episode 34 gave: zero is a claim that the set is free.
        Assert.Null(detail.StartingPrice);
        Assert.Empty(detail.Copies);
    }

    private async Task<SetDetailResponse> GetDetailAsync(HttpClient client, Guid setId)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/v1/catalog/sets/{setId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        SetDetailResponse? detail =
            await response.Content.ReadFromJsonAsync<SetDetailResponse>(Database.Api.Json);

        Assert.NotNull(detail);

        return detail;
    }
}
```

Red, and it is a build error: `The type or namespace name 'SetDetailResponse' could not be found`.
That is a legitimate red. The test has just decided what the response is called and what it carries.

The route goes into the **public** group in `BrowseEndpoints`, not into the staff group
`/catalog/sets` that already has the `POST`. Both give the same URL, `/api/v1/catalog/sets/{id}`.
**The group decides who may call it.** Episode 40 secures the staff groups with one line each, and
browse stays anonymous. A customer-facing `GET` inside a staff group would become a route that
requires a login, and nobody would notice until a customer did.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in MapBrowse
        return group;
    }
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly above it
        group.MapGet("/sets/{setId:guid}", GetSetAsync);

```

The handler reads the set from **episode 34's view**, `catalog_set_listings`. The view already has
every product fact the listing shows, plus the available count and the starting price. The
listing and this page then read the same row, so they can't disagree:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, already there
    private static IQueryable<CatalogSetListing> WhereEveryFilterButTheme(
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly above it
    private static async Task<Ok<SetDetailResponse>> GetSetAsync(
        Guid setId,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        CatalogSetListing listing = await database.Listings
            .SingleAsync(candidate => candidate.Id == setId, cancellationToken);

        return TypedResults.Ok(new SetDetailResponse(
            listing.Id,
            listing.SetNumber,
            listing.Name,
            listing.ThemeName,
            listing.Year,
            listing.PieceCount,
            listing.MinimumAge,
            listing.MinimumRentalDays,
            listing.AvailableCount,
            listing.StartingPrice,
            []));
    }

```

And the response, at the bottom of the file with the others:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, the last line of the file
public sealed record ThemeFacetResponse(Guid Id, string Name, int SetCount);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly below it

public sealed record SetDetailResponse(
    Guid Id,
    string SetNumber,
    string Name,
    string Theme,
    int Year,
    int PieceCount,
    int MinimumAge,
    int MinimumRentalDays,
    int AvailableCount,
    decimal? StartingPrice,
    IReadOnlyList<SetCopyResponse> Copies);

public sealed record SetCopyResponse(Guid Id, ConditionGrade Grade, bool Available);
```

`ConditionGrade` needs `using BrickShare.Catalog.Domain;` at the top of `BrowseEndpoints.cs`.

`SetCopyResponse` is declared a step early, because the list needs an element type. Its three
fields come from UC-7.4 and UC-7.5, and step 3 is the test that actually fills it. `Copies` is the
literal `[]` for now. That is the smallest thing that passes, and the next test will not let it stay.

Green.

**Caveman version:** customer ask "show me Titanic". Shop have zero Titanic box. Server still show
Titanic, all facts, price say "none". Not hide. Not say "free".

## Step 2 — Red → green: a set nobody catalogued

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — the anchor, already there
    private async Task<SetDetailResponse> GetDetailAsync(HttpClient client, Guid setId)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — paste directly above it
    [Fact]
    public async Task A_set_nobody_catalogued_is_a_404_that_says_so()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync($"/api/v1/catalog/sets/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("not in the catalog", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

```

Red: `Expected: NotFound, Actual: InternalServerError`. `SingleAsync` throws on an empty result, and
episode 22's `UseExceptionHandler` turns any exception into a `500`.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in GetSetAsync
    private static async Task<Ok<SetDetailResponse>> GetSetAsync(
        Guid setId,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        CatalogSetListing listing = await database.Listings
            .SingleAsync(candidate => candidate.Id == setId, cancellationToken);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
    private static async Task<Results<Ok<SetDetailResponse>, ProblemHttpResult>> GetSetAsync(
        Guid setId,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        CatalogSetListing? listing = await database.Listings
            .SingleOrDefaultAsync(candidate => candidate.Id == setId, cancellationToken);

        if (listing is null)
        {
            return TypedResults.Problem(
                title: "No such set",
                detail: $"Set {setId} is not in the catalog.",
                statusCode: StatusCodes.Status404NotFound);
        }
```

Green.

**Why the test reads the body, and not only the status.** Step 1's red was *also* a `404`: the route
did not exist yet, and ASP.NET Core answered with an empty body. **A routing 404 and a "no such set"
404 have the same status code.** A test that checks only the status would have passed back in step 1,
before the route existed, for the wrong reason. The detail string is how the test knows *this
endpoint* said no. Step 7 depends on the same difference.

**Caveman version:** customer ask for set that not exist. Before: server fall over, shout 500. Now:
server say calmly "no such set", and say it in words, so nobody confuse it with "no such door".

## Step 3 — Red → green: every copy, including the one on rent

This is the rule the episode is named after:

> Every copy is listed, **including unavailable ones**. Customers need to see a copy in order
> to subscribe to it, and to understand what the set will look like when one frees up.

Two copies. The Fair one goes out on rent through the domain, using the helper from episode 34, because
the endpoints that would do it belong to a rentals service that does not exist yet:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — the anchor, already there
    private async Task<SetDetailResponse> GetDetailAsync(HttpClient client, Guid setId)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — paste directly above it
    [Fact]
    public async Task Every_copy_is_listed_including_one_out_on_rent()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        Guid excellent = await Database.RegisterCopyAsync(client, setId, "Excellent");
        Guid fair = await Database.RegisterCopyAsync(client, setId, "Fair");
        await SendOnRentAsync(fair);

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        Assert.Equal(1, detail.AvailableCount);
        Assert.Equal(2, detail.Copies.Count);
        Assert.True(detail.Copies.Single(copy => copy.Id == excellent).Available);

        // The copy a customer subscribes to (UC-7.5). Filter it out and the subscribe button has
        // nothing to point at.
        Assert.False(detail.Copies.Single(copy => copy.Id == fair).Available);
    }

```

And the helper, at the bottom of the class:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — paste directly below GetDetailAsync

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

Red: `Assert.Equal() Failure: Expected: 2, Actual: 0`. The literal `[]` from step 1 has done its job.

### The version intuition writes first

**Write this on camera, and run it.** It is the query nearly everyone writes first, because the
page is "about renting":

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — in GetSetAsync, above the return. DO NOT KEEP
        List<Copy> copies = await database.Copies
            .AsNoTracking()
            .Where(copy => copy.CatalogSetId == setId && copy.Status == CopyStatus.Available)
            .ToListAsync(cancellationToken);
```

Pass `[.. copies.Select(copy => new SetCopyResponse(copy.Id, copy.Grade, true))]` as the last
argument, and run the test. Red: `Expected: 2, Actual: 1`. **This is not a straw man.** It looks
correct and reads well. A customer using it sees one copy and doesn't know the other exists. It
fails no other test in the suite. And UC-7.5's subscribe path disappears without an error anywhere.

The fix is to remove a condition, not add one:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — in GetSetAsync, directly above the return
        // Every copy, available or not: a customer subscribes to the ones that are not (UC-7.5).
        // Read-only, so nothing is tracked. Nothing here will be saved.
        List<Copy> copies = await database.Copies
            .AsNoTracking()
            .Where(copy => copy.CatalogSetId == setId)
            .OrderBy(copy => copy.Id)
            .ToListAsync(cancellationToken);

```

And the last argument of the return becomes:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, the last argument in GetSetAsync's return
            []));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
            [.. copies.Select(copy => new SetCopyResponse(
                copy.Id,
                copy.Grade,
                copy.Status == CopyStatus.Available))]));
```

Green. **Availability moved from the `WHERE` clause into a field.** That one move is the whole
lesson: the query decides what is **shown**, and the field decides what can be **acted on**.

### A judgement call: `available`, not the status

The customer gets a boolean, not `CopyStatus`. The raw status would tell them more: *in inspection*
means "soon", and *on rent* means "not for a while". It is a real loss, and a reasonable team could ship
the status.

It is not shipped here because `CopyStatus` is the **catalog's internal workflow**:
`AwaitingInspection`, `InInspection`, `InRepair`, `Lost`. Putting it on a public response makes
every future change to that workflow a breaking change for customers. It would also tell the world
which boxes the shop has lost. **The customer needs to know one thing, whether they can reserve
this copy, and that is the field.** If "back soon" turns out to matter, it becomes its own field,
designed for the customer, and not a leak of the state machine.

`Lost` copies are listed, as unavailable. UC-5 recovers lost sets, so a subscriber to one may yet get
it. `Retired` is different, and step 5 deals with it.

**Caveman version:** first idea: show only free box. Looks right. Is wrong. Customer can't wait for
box they can't see. So show all box. Put little flag on each: "can take now: yes/no".

## Step 4 — Red → green: each copy's price and deposit

UC-7.4 is *"the screen where the customer decides whether a worn copy at a lower price is the better
deal"*. For that, each copy needs its own numbers. The test also decides the order:
**cheapest first**, so the list starts with the number the listing called the starting price.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — the anchor, already there
    private async Task<SetDetailResponse> GetDetailAsync(HttpClient client, Guid setId)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — paste directly above it
    [Fact]
    public async Task Each_copy_carries_its_own_price_and_deposit_cheapest_first()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        await Database.RegisterCopyAsync(client, setId, "Excellent");
        await Database.RegisterCopyAsync(client, setId, "Fair");

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        // 60.00 base and 629.99 retail, times 0.55 for Fair and 0.85 for Excellent. 346.4945 and
        // 535.4915 round to the cent the way Money does. Registered Excellent first, listed Fair first.
        Assert.Equal(
            [(ConditionGrade.Fair, 33.00m, 346.49m), (ConditionGrade.Excellent, 51.00m, 535.49m)],
            detail.Copies.Select(copy => (copy.Grade, copy.RentalPrice, copy.Deposit)));
    }

```

Red, a build error: `'SetCopyResponse' does not contain a definition for 'RentalPrice'`.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, at the bottom of the file
public sealed record SetCopyResponse(Guid Id, ConditionGrade Grade, bool Available);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
public sealed record SetCopyResponse(
    Guid Id,
    ConditionGrade Grade,
    bool Available,
    decimal RentalPrice,
    decimal Deposit);
```

**This is the first request in the service that runs `PriceCalculator`.** It has been unit-tested
since episode 12, and nothing has called it outside those tests. It needs three things. The first is
the set's two prices, which the view does not carry. The second is the multipliers from episode 34's
seeded table. The third is the copies. This is `GetSetAsync` in full, from the null check down:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in GetSetAsync, everything after the null check
        // Every copy, available or not: a customer subscribes to the ones that are not (UC-7.5).
        // Read-only, so nothing is tracked. Nothing here will be saved.
        List<Copy> copies = await database.Copies
            .AsNoTracking()
            .Where(copy => copy.CatalogSetId == setId)
            .OrderBy(copy => copy.Id)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new SetDetailResponse(
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes, down to the return
        // The two prices the view does not carry. Projected, because nothing else on the set is needed.
        var prices = await database.Sets
            .Where(set => set.Id == setId)
            .Select(set => new { set.BaseRentalPrice, set.RetailPrice })
            .SingleAsync(cancellationToken);

        Dictionary<ConditionGrade, decimal> rows = await database.GradeMultipliers
            .ToDictionaryAsync(row => row.Grade, row => row.Multiplier, cancellationToken);

        GradeMultipliers multipliers = new(rows);

        // Every copy, available or not: a customer subscribes to the ones that are not (UC-7.5).
        // Read-only, so nothing is tracked. Nothing here will be saved.
        List<Copy> copies = await database.Copies
            .AsNoTracking()
            .Where(copy => copy.CatalogSetId == setId)
            .ToListAsync(cancellationToken);

        // Cheapest first. Id breaks a tie between two copies of the same grade, so the order is
        // the same on every request.
        List<SetCopyResponse> onShow =
        [
            .. copies
                .Select(copy => new SetCopyResponse(
                    copy.Id,
                    copy.Grade,
                    copy.Status == CopyStatus.Available,
                    PriceCalculator.RentalPrice(prices.BaseRentalPrice, copy.Grade, multipliers).Amount,
                    PriceCalculator.Deposit(prices.RetailPrice, copy.Grade, multipliers).Amount))
                .OrderBy(copy => copy.RentalPrice)
                .ThenBy(copy => copy.Id)
        ];

        return TypedResults.Ok(new SetDetailResponse(
```

The return's last argument, the `[.. copies.Select(…)]` from step 3, becomes `onShow`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what the end of GetSetAsync's return becomes
            listing.StartingPrice,
            onShow));
```

`GradeMultipliers` and `PriceCalculator` need `using BrickShare.Catalog.Domain.Pricing;`.

Green. Three lines are worth a sentence each:

- **The order is in C#, not in SQL.** The price is computed here, from `Money`, so it can only be
  sorted here. Sorting in SQL would need the pricing formula in SQL a second time, and there is
  already one copy of it too many (next test).
- **`new GradeMultipliers(rows)` is a guard, not just a conversion.** Its constructor refuses a table
  that is missing a grade (episode 12). If a bad edit deletes a row, the page fails loudly instead of
  pricing a copy at zero.
- **Four round trips per request:** the view, the set's prices, the multipliers, and the copies. Adding
  the two prices to the view would save one of them. It would also mean a migration that drops and
  recreates the view, as episode 34 warned. That is not worth it for one indexed lookup by primary key,
  on a page a customer opens one set at a time.

### The test that passes the moment it is written

Episode 34 said *"the pricing formula now exists twice, in `PriceCalculator` and in the view"*. It
pinned them with literal numbers. This page is the first place both are computed **in the same
response**, so the pin can now be direct:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — the anchor, already there
    private async Task<SetDetailResponse> GetDetailAsync(HttpClient client, Guid setId)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — paste directly above it
    [Fact]
    public async Task The_starting_price_is_the_price_of_the_cheapest_copy_a_customer_can_reserve()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        await Database.RegisterCopyAsync(client, setId, "Excellent");
        Guid fair = await Database.RegisterCopyAsync(client, setId, "Fair");
        await SendOnRentAsync(fair);

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        SetCopyResponse cheapestAvailable = detail.Copies
            .Where(copy => copy.Available)
            .MinBy(copy => copy.RentalPrice)!;

        // Two sources for the same number: the view's SQL and PriceCalculator's C#. If they ever
        // disagree, a customer is shown one price in the list and another on this page.
        Assert.Equal(51.00m, detail.StartingPrice);
        Assert.Equal(cheapestAvailable.RentalPrice, detail.StartingPrice);
    }

```

Green immediately. **It describes a rule, and it drives nothing.** It is kept because it is the
cheapest possible alarm on the one cost episode 34 accepted. Change the rounding in `Money`, or
change the view to `round(…, 1)`, and this test is the one that goes red.

**Caveman version:** each box get own price and own deposit, worn box cheaper. Cheapest box on
top. Then check: price on top of page same as price in list page. Two brain do same sum. Test make
sure two brain agree.

## Step 5 — Red → green: a retired copy is not on show

Step 3 said *every copy*. There is one exception, and `docs/architecture/catalog.md` says it in
passing: a set with no copies may be *"a set whose every copy has been retired"*. **A retired copy is
not a copy the shop has.** It is a row kept for history, as episode 31 made sure. Nobody can reserve
it, and nobody should subscribe to it, because it is never coming back.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — the anchor, already there
    private async Task<SetDetailResponse> GetDetailAsync(HttpClient client, Guid setId)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/SetDetailTests.cs — paste directly above it
    [Fact]
    public async Task A_retired_copy_is_not_on_show()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        Guid kept = await Database.RegisterCopyAsync(client, setId, "New");
        Guid retired = await Database.RegisterCopyAsync(client, setId, "Fair");

        HttpResponseMessage retirement = await client.PostAsync(
            $"/api/v1/catalog/copies/{retired}/retirement", content: null);

        retirement.EnsureSuccessStatusCode();

        SetDetailResponse detail = await GetDetailAsync(client, setId);

        Assert.Equal([kept], detail.Copies.Select(copy => copy.Id));
    }

```

Retirement goes through the real endpoint from episode 31, not through the domain. That endpoint
exists, so the test uses the front door.

Red: `Expected: [kept], Actual: [retired, kept]`. The Fair copy is cheaper, so it is listed first,
which makes the failure easy to read.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in GetSetAsync
        // Every copy, available or not: a customer subscribes to the ones that are not (UC-7.5).
        // Read-only, so nothing is tracked. Nothing here will be saved.
        List<Copy> copies = await database.Copies
            .AsNoTracking()
            .Where(copy => copy.CatalogSetId == setId)
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        // Every copy the shop rents out, available or not: a customer subscribes to the ones that
        // are not (UC-7.5). Retired is the one exception. It is history, not stock.
        // Read-only, so nothing is tracked. Nothing here will be saved.
        List<Copy> copies = await database.Copies
            .AsNoTracking()
            .Where(copy => copy.CatalogSetId == setId && copy.Status != CopyStatus.Retired)
```

Green. **This is the only status that decides what is shown.** It works because *retired* is not
about availability. It is about whether the box still belongs to the rental stock at all. Retire every
copy of the Titanic and the page looks exactly like step 1's: the set in full, no copies, and a null
price. The first rule and this one agree.

### Refactor: two things that would be easy to get wrong later

**The multipliers get a name.** Episode 40's two-phase multiplier edit will need to load the same
table into the same type. Two lines inline become one call:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in GetSetAsync
        Dictionary<ConditionGrade, decimal> rows = await database.GradeMultipliers
            .ToDictionaryAsync(row => row.Grade, row => row.Multiplier, cancellationToken);

        GradeMultipliers multipliers = new(rows);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        GradeMultipliers multipliers = await LoadGradeMultipliersAsync(database, cancellationToken);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, already there
    private static IQueryable<CatalogSetListing> WhereEveryFilterButTheme(
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly above it
    /// <summary>
    /// Episode 34's seeded table as the type PriceCalculator takes. The constructor refuses a table
    /// with a grade missing, so a bad edit fails here instead of pricing a copy at zero.
    /// </summary>
    private static async Task<GradeMultipliers> LoadGradeMultipliersAsync(
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        Dictionary<ConditionGrade, decimal> rows = await database.GradeMultipliers
            .ToDictionaryAsync(row => row.Grade, row => row.Multiplier, cancellationToken);

        return new GradeMultipliers(rows);
    }

```

**And one line that is deliberately *not* written.** The handler now holds every copy and its
price, in memory. Working out the starting price and the available count right here is one line of
LINQ each, and it saves reading them from the view:

```csharp
// NOT WRITTEN. What the refactor is tempted to do:
decimal? startingPrice = onShow.Where(copy => copy.Available).Min(copy => (decimal?)copy.RentalPrice);
```

It would pass every test in this file. It would also be the **third** copy of the starting-price rule,
after `PriceCalculator` and the view, and the only one that the listing does not use. The day
someone changes one of them, the list says 51.00 and the detail says 50.99. **The per-set numbers
have one source, and it is the view.** The comment above `GetSetAsync`'s first query says so:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, the first query in GetSetAsync
        CatalogSetListing? listing = await database.Listings
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly above it
        // availableCount and startingPrice come from the view the listing reads, never from the
        // copies below, so the list and this page cannot disagree about a price (episode 34).
```

Green, still.

**Caveman version:** retired box not in shop anymore, only in history book. Don't show. Then: don't
compute cheapest price again from box in hand. List page already has it. One place for that number,
or two page will one day argue.

## Step 6 — The sentence that ties it together, and describing it

**Not driven by a test.** An argument, then metadata and requests.

The three rules so far are: a set with no copies shows in full, every copy is listed including one on
rent, and a retired copy is not. They look like three separate things to remember. They are one:

> ***Available* controls what can be acted on, never what is shown.**

Every bug this half of the episode prevented comes from breaking that sentence. Step 1's bug hides a
set because nothing can be rented. Step 3's bug hides a copy because it is out. Step 5 is not an
exception to the sentence. Retired is not a kind of unavailable, it means *not stock*. **Write the
sentence down.** It prevents all three bugs faster than the tests catch them.

### The rules that are not here yet

- **Only published photographs are shown to customers.** This is the fourth rule. It is a **privacy
  rule about a named person**: an evidence photograph documents one customer's damage dispute. It
  arrives with the photographs, in episode 39, as a `photos` list on each copy with a test that an
  evidence photograph is never in it. It will not be a flag on this query that a caller could flip.
  There will be two paths, and the staff one lives on the staff side.
- **The product image.** Rebrickable's image URL is on the `RebrickableSnapshot` from episode 27, not
  on `catalog_sets`, and it is a hotlink to somebody else's CDN. Whether the shop serves its own
  product image goes with the other images in episodes 38–39.
- **Completeness.** UC-7.4 asks how complete each copy is. Nothing records that yet, because it is
  what an inspection produces, and inspections are a later service.

### Describe it

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in MapBrowse
        group.MapGet("/sets/{setId:guid}", GetSetAsync);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        group.MapGet("/sets/{setId:guid}", GetSetAsync)
            .WithSummary("View a set and every copy of it")
            .WithDescription(
                "Product facts, with availableCount and startingPrice as the listing shows them. "
                + "copies lists every copy the shop rents out, available or not, cheapest first, "
                + "each with its own rentalPrice and deposit. available says whether a copy can be "
                + "reserved now; every copy listed can be subscribed to. A set with no copies has "
                + "an empty list and a null startingPrice. Retired copies are not listed.")
            .ProducesProblem(StatusCodes.Status404NotFound);
```

Then the requests, appended at the end of `BrickShare.Catalog.Api.http`:

```http
### BrickShare.Catalog.Api.http — append at the end of the file

### Set detail. Paste an id from the browse response above.
@setId = paste-a-set-id-here

### Every copy, available or not, cheapest first, each with its own price and deposit.
GET {{host}}/api/v1/catalog/sets/{{setId}}

### A set nobody catalogued: 404, and a body that says so.
GET {{host}}/api/v1/catalog/sets/00000000-0000-0000-0000-000000000000
```

Run the first request against a set with a copy on rent, if Compose has one, and hold on the copy
with `"available": false`. That copy is the reason the episode exists.

**Caveman version:** one sentence rule them all: "free" say what you can grab, not what you can see.
Photo rule come later, with photo.

---

> **Seam — 37a ends here.** If recording in two parts: every test is green, and a customer can open a
> set and compare its copies. 37b opens on a sentence from episode 21: *"`TypedResults.Created`
> sets a `Location` header pointing at `GET /api/v1/sets/{id}` — and that route does not exist
> until episode 37."* It exists now. Follow it.

---

# 37b — The scan, and headers that point somewhere

## Step 7 — Red → green: follow the catalogue `Location`

Episodes 21, 28 and 30 each set a `Location` header on a `201` and said the route behind it would
arrive later. It has arrived. Nothing has ever followed one of those headers, so the first test
does exactly that:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — the anchor, already there
    [Fact]
    public async Task An_empty_request_is_refused_field_by_field()
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — paste directly above it
    [Fact]
    public async Task The_location_of_a_catalogued_set_is_somewhere_a_client_can_go()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid lookupId = await Database.LookUpAsync(client, StockedSet.Titanic);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId));

        Assert.NotNull(created.Headers.Location);

        HttpResponseMessage followed = await client.GetAsync(created.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);

        SetDetailResponse? detail =
            await followed.Content.ReadFromJsonAsync<SetDetailResponse>(Database.Api.Json);

        Assert.NotNull(detail);
        Assert.Equal("Titanic", detail.Name);
    }

```

The expectation, said out loud before running: *green, because the route exists now.* Run it.

Red: `Expected: OK, Actual: NotFound`.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — the anchor, in CatalogueAsync
        return TypedResults.Created($"/api/v1/sets/{catalogSet.Id}", CatalogSetResponse.From(catalogSet, theme));
```

**`/api/v1/sets/…`, with no `/catalog`.** It has been wrong since episode 21. The route group was
`/catalog/sets` all along, and the header was written from memory. It survived sixteen episodes
because the route it pointed at didn't exist, so a wrong URL and a right URL for a missing route
failed in exactly the same way. That is step 2's point from the other direction: **a 404 from a
route that is not there hides every other kind of wrong.**

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — what it becomes
        return TypedResults.Created(
            $"/api/v1/catalog/sets/{catalogSet.Id}", CatalogSetResponse.From(catalogSet, theme));
```

Green.

**A header nobody follows is a header nobody tests.** The `201` tests since episode 21 checked the
status and the body, and never the `Location`, because there was nowhere to go. The fix is one
string. The lesson is that **a string an API promises to clients needs a test that uses it the way a
client would.**

**Caveman version:** sign on door say "Titanic in room 5". Nobody walk to room 5 before, because room
not built. Room built now. Walk there. Sign wrong: say "room 5" on wrong floor. Fix sign. Now test
walk there every time.

## Step 8 — Red → green: the `Location` of a delivery

Registration's header is the next one:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RegisterCopiesTests.cs — the anchor, already there
    [Fact]
    public async Task Seventeen_boxes_arrive_in_one_call_and_get_seventeen_labels()
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/RegisterCopiesTests.cs — paste directly above it
    [Fact]
    public async Task The_location_of_a_delivery_lists_every_box_in_it()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { Weighing(9200), Weighing(9187) } });

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);

        HttpResponseMessage followed = await client.GetAsync(response.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);

        SetDetailResponse? detail =
            await followed.Content.ReadFromJsonAsync<SetDetailResponse>(Database.Api.Json);

        Assert.NotNull(detail);
        Assert.Equal(
            registered.Copies.Select(copy => copy.Id).Order(),
            detail.Copies.Select(copy => copy.Id).Order());
    }

```

The ids are compared as sets, with `.Order()` on both sides. The detail lists copies cheapest first,
and these two cost the same, so the comparison can't depend on their order.

Red: `Expected: OK, Actual: MethodNotAllowed`. **A different failure, and it says something different.**
`405` means the path exists and the method does not. `/catalog/sets/{setId}/copies` is the
registration route, which only accepts `POST`.

Episode 30 left this header pointing at `GET /catalog/sets/{setId}/copies` and promised that route
for this episode. **It is not built, and the promise changes.** The set detail from 37a already lists
every copy of the set, which is exactly what a client following a delivery wants to see. A second
endpoint returning the same list, with its own tests, its own OpenAPI entry and its own chance to
disagree, would be symmetry that nobody asked for. **The header points at the resource that now shows
what was created.**

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the anchor, at the end of RegisterAsync
        return TypedResults.Created(
            $"/api/v1/catalog/sets/{setId}/copies",
            new RegisterCopiesResponse([.. copies.Select(CopyResponse.From)]));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — what it becomes
        // Seventeen copies have no single location. This points at the set they now belong to,
        // whose detail lists every copy of it (episode 37).
        return TypedResults.Created(
            $"/api/v1/catalog/sets/{setId}",
            new RegisterCopiesResponse([.. copies.Select(CopyResponse.From)]));
```

Green.

**The honest cost.** The detail is the *customer's* view. A staff member following the header sees
grades, prices and availability, but not label codes or weights. That's fine today: the
registration response already carried every label, in delivery order, because episode 30 made that
the point. If staff ever need to re-read a delivery's labels later, it becomes a staff endpoint,
built when someone asks for it.

**Caveman version:** sign after new box delivery point to door that only take box in, not show box
out. Server say 405: "door exist, wrong way". Point sign at Titanic room instead. Room already show
all box.

## Step 9 — Red → green: scan a label

UC-1.4: a staff member holds a box, scans the sticker, and needs to know what it is. Episode 31
put retirement under `/catalog/copies` rather than under a set, because *"the nesting follows the
operation"*. The person at the counter has a copy and nothing else. The scan belongs in that same
group.

A new test file. Its helper registers through the front door and returns the whole `CopyResponse`,
because the label code is the thing this file scans:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/ScanLabelTests.cs — new file
using System.Net;
using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BrickShare.Catalog.IntegrationTests.Copies;

public class ScanLabelTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task Scanning_a_label_finds_the_box_and_the_set_it_belongs_to()
    {
        HttpClient client = Database.Api.CreateClient();
        CopyResponse registered = await RegisterOneCopyAsync(client);

        ScannedCopyResponse scanned = await ScanAsync(client, registered.LabelCode);

        Assert.Equal(registered.Id, scanned.Id);
        Assert.Equal("10294-1", scanned.SetNumber);
        Assert.Equal("Titanic", scanned.SetName);
        Assert.Equal(ConditionGrade.New, scanned.Grade);
        Assert.Equal(CopyStatus.Available, scanned.Status);
        Assert.Null(scanned.RetiredAt);
    }

    [Fact]
    public async Task A_label_nobody_minted_is_a_404_that_says_so()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/catalog/copies/by-label/BRK-222222");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("BRK-222222", problem.Detail);
    }

    private async Task<ScannedCopyResponse> ScanAsync(HttpClient client, string code)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/v1/catalog/copies/by-label/{code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        ScannedCopyResponse? scanned =
            await response.Content.ReadFromJsonAsync<ScannedCopyResponse>(Database.Api.Json);

        Assert.NotNull(scanned);

        return scanned;
    }

    private async Task<CopyResponse> RegisterOneCopyAsync(HttpClient client)
    {
        Guid setId = await Database.CatalogueAsync(client, StockedSet.Titanic);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { new { grade = "New", baselineWeightInGrams = 9200 } } });

        response.EnsureSuccessStatusCode();

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(Database.Api.Json);

        Assert.NotNull(registered);

        return Assert.Single(registered.Copies);
    }
}
```

Two tests, because step 2 already taught the second one: a 404 has to say *which* thing was not
found, or a missing route passes it. Red, a build error: `'ScannedCopyResponse' could not be found`.

The route, in the `/catalog/copies` group:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the anchor, in MapCopyRetirements
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — what it becomes
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/by-label/{code}", ScanAsync);

        return group;
```

No validation filter, and **episode 30's decision pays off here.** The filter was attached per
endpoint, not to the group, and a `GET` with no body would have thrown with a group-wide filter.

The handler, below `RetireAsync`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the anchor, already there
    private static async Task<IReadOnlyList<Copy>> RegisterWithMintedLabelsAsync(Guid setId,
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — paste directly above it
    private static async Task<Results<Ok<ScannedCopyResponse>, ProblemHttpResult>> ScanAsync(
        string code,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        LabelCode label = LabelCode.Parse(code);

        var scanned = await database.Copies
            .AsNoTracking()
            .Where(copy => copy.Label == label)
            .Join(database.Sets,
                copy => copy.CatalogSetId,
                set => set.Id,
                (copy, set) => new { Copy = copy, set.Number, set.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (scanned is null)
        {
            return TypedResults.Problem(
                title: "No such label",
                detail: $"No copy carries the label {label}. Check the sticker against the box.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(ScannedCopyResponse.From(scanned.Copy, scanned.Number, scanned.Name));
    }

```

And the response, at the bottom of the file:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the anchor, the end of the file
        copy.BaselineWeightInGrams);
}
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — paste directly below it

/// <summary>
/// What staff see when they scan a box: the copy, which set it is a copy of, and where it is in
/// its life. Unlike the customer's view, this carries the raw status.
/// </summary>
public sealed record ScannedCopyResponse(
    Guid Id,
    Guid CatalogSetId,
    string SetNumber,
    string SetName,
    string LabelCode,
    ConditionGrade Grade,
    CopyStatus Status,
    int BaselineWeightInGrams,
    DateTimeOffset? RetiredAt)
{
    public static ScannedCopyResponse From(Copy copy, SetNumber setNumber, string setName) => new(
        copy.Id,
        copy.CatalogSetId,
        setNumber.Value,
        setName,
        copy.Label.Value,
        copy.Grade,
        copy.Status,
        copy.BaselineWeightInGrams,
        copy.RetiredAt);
}
```

Green, both.

**The raw status, here and not in step 3, and that is not a contradiction.** The customer got a
boolean because the workflow is not their contract. Staff *are* the workflow. The person scanning
a box needs to know whether it is `AwaitingInspection` or `InRepair`, because that decides what they
do with it next. **The same fact has two audiences, so it gets two shapes.** It also gets two routes
in two groups. That is how episode 40 can make one of them staff-only without touching the other.

**Two details in the handler:**

- **`copy.Label == label` compares two `LabelCode`s**, and EF runs the comparison through the value
  converter from episode 16. The SQL is `where label_code = @label`, which hits the unique index
  `ix_copies_label_code`. The scan is an index lookup, and so is the join on the primary key.
- **The join happens in the query and the mapping happens in `From`.** `SetNumber` is a value
  object, so its `.Value` is read in C#, after Postgres has done the work. This is the same split
  `CopyResponse.From` has used since episode 29.

### Refactor: the group's name is a lie now

`MapCopyRetirements` maps retirement and the scan. The name describes the first route it held, and
it will hold regrading in episode 40's neighbourhood too. **A name that describes one member of a
group misleads the next person who reads `Program.cs`.**

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the anchor
    public static RouteGroupBuilder MapCopyRetirements(this IEndpointRouteBuilder routes)
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — what it becomes
    public static RouteGroupBuilder MapCopyOperations(this IEndpointRouteBuilder routes)
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — the anchor
v1.MapCopyRetirements();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — what it becomes
v1.MapCopyOperations();
```

A rename: wiring with no behaviour of its own. Every test is still green. That is the only
evidence a rename needs.

**Caveman version:** staff hold box, scan sticker. Server look up sticker, say: this Titanic box,
New, sitting on shelf. Staff get full truth, customer get simple truth. Same box, two way to say it.

## Step 10 — Red → green: a label read down a phone

Episode 13 designed the label alphabet around one fact: *"this code is printed on a box, scanned at
a counter, and read down a phone when the scanner will not read it."* So the codes contain no `0`, `O`,
`1`, `I`, `L` or `U`. A person reading `BRK-0OOOOO` down the phone has made a mistake the alphabet
was built to catch, and the endpoint should say so:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/ScanLabelTests.cs — the anchor, already there
    private async Task<ScannedCopyResponse> ScanAsync(HttpClient client, string code)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/ScanLabelTests.cs — paste directly above it
    [Fact]
    public async Task A_code_that_cannot_be_a_label_is_refused_with_the_reason()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/catalog/copies/by-label/BRK-0OOOOO");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        HttpValidationProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("code", problem.Errors.Keys);
    }

```

Red: `Expected: BadRequest, Actual: InternalServerError`. `LabelCode.Parse` throws `FormatException`,
and nothing turns that into anything but a `500`.

**Why `400` and not `404`.** "No copy has this label" would be true, and it is a `404`. But it sends
the staff member to look for a box that doesn't exist, when what went wrong is that they typed it wrong.
A code that *can't* be a label is a malformed request. It gets the same `ValidationProblem` shape as
every other `400` in this API, keyed by the field that was wrong:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the anchor, the top of ScanAsync
    private static async Task<Results<Ok<ScannedCopyResponse>, ProblemHttpResult>> ScanAsync(
        string code,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        LabelCode label = LabelCode.Parse(code);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — what it becomes
    private static async Task<Results<Ok<ScannedCopyResponse>, ValidationProblem, ProblemHttpResult>>
        ScanAsync(
            string code,
            CatalogDbContext database,
            CancellationToken cancellationToken)
    {
        // A code that cannot be a label is a typing mistake, not a missing box. Say which.
        if (!LabelCode.TryParse(code, out LabelCode? label))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["code"] =
                [
                    $"'{code}' is not a BrickShare label. Labels look like BRK-7KQ2XM, "
                    + "and never contain 0, O, 1, I, L or U."
                ]
            });
        }
```

Green. The check lives in `LabelCode.TryParse`, the domain's own definition of a label from
episode 13. The endpoint does not repeat the regular expression. It asks the type.

### Two tests that pass the moment they are written

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/ScanLabelTests.cs — the anchor, already there
    private async Task<ScannedCopyResponse> ScanAsync(HttpClient client, string code)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Copies/ScanLabelTests.cs — paste directly above it
    [Fact]
    public async Task A_label_typed_in_lowercase_still_finds_the_box()
    {
        HttpClient client = Database.Api.CreateClient();
        CopyResponse registered = await RegisterOneCopyAsync(client);

        ScannedCopyResponse scanned = await ScanAsync(client, registered.LabelCode.ToLowerInvariant());

        Assert.Equal(registered.Id, scanned.Id);
    }

    [Fact]
    public async Task A_retired_box_still_scans_and_says_it_is_retired()
    {
        HttpClient client = Database.Api.CreateClient();
        CopyResponse registered = await RegisterOneCopyAsync(client);

        HttpResponseMessage retirement = await client.PostAsync(
            $"/api/v1/catalog/copies/{registered.Id}/retirement", content: null);

        retirement.EnsureSuccessStatusCode();

        ScannedCopyResponse scanned = await ScanAsync(client, registered.LabelCode);

        Assert.Equal(CopyStatus.Retired, scanned.Status);
        Assert.NotNull(scanned.RetiredAt);
    }

```

Both green immediately. **They describe rules and drive nothing**, and each one is worth a sentence:

- **Lowercase works because `LabelCode.TryParse` normalises**, as it has since episode 13. The test
  pins that the endpoint goes through the type, and doesn't compare raw strings.
- **A retired box scans.** Compare this with step 5, where a retired copy is not shown to customers.
  There is no contradiction, because the two audiences ask different questions. A customer asks
  *what can I rent?*, and a retired box is not an answer to that. A staff member holding a retired box
  asks *what is this?*, and "retired on 12 March" is exactly the answer. A scan that returned `404`
  for a box with a BrickShare sticker on it would be a lie.

**Caveman version:** staff read sticker on phone, say "zero" where no zero exist. Server not say
"no such box". Server say "that not a sticker word, stickers never have zero". Small letter fine.
Old retired box fine too: server say "this box retired".

## Step 11 — Describe it, and run it

**Not driven by a test.** Metadata and requests.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — the anchor, in MapCopyOperations
        group.MapGet("/by-label/{code}", ScanAsync);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — what it becomes
        group.MapGet("/by-label/{code}", ScanAsync)
            .WithSummary("Find a copy by scanning its label")
            .WithDescription(
                "Finds the copy in any status, Retired included, with the set it belongs to. "
                + "Case and surrounding spaces do not matter. A code that cannot be a label is a "
                + "400, not a 404.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
```

**The description does not say *staff only*.** Until episode 40, anyone who can reach the service can
call it. Episode 32 set the rule that nothing in the document may claim something the service does not
enforce. Episode 40 adds the claim when it adds the enforcement.

The requests, appended at the end of `BrickShare.Catalog.Api.http`:

```http
### BrickShare.Catalog.Api.http — append at the end of the file

### Scan a label. Paste one from a registration response. Lowercase works too.
@label = BRK-paste-here

GET {{host}}/api/v1/catalog/copies/by-label/{{label}}

### Read down the phone with an O for a 0: 400, and the message says why.
GET {{host}}/api/v1/catalog/copies/by-label/BRK-0OOOOO
```

Then **follow a `Location` by hand.** Run episode 21's catalogue request, copy the `Location` from
the response headers, and open it. The detail comes back. It is the same header that had pointed at
nothing since episode 21.

**Caveman version:** map say what scan does, and not say "staff only" yet, because lock not on
door yet. Walk the sign by hand once. Sign true now.

---

## What this episode is not

**No photographs.** This is the change from the course plan named at the top. They arrive in
episode 38, and the published-only rule arrives with its test in episode 39.

**No product image, no completeness.** Step 6 says why: one goes with the images, and the other goes
with inspections.

**No `GET /catalog/sets/{setId}/copies`.** Episode 30 promised it, and step 8 explains why the set
detail is the better place for that header to point.

**No `GET /catalog/copies/{id}`.** The person holding a box has a label, not a Guid. Every id a
client holds came from a response that already carried the copy. It gets built when a client exists
that holds an id and nothing else.

**The lookup's `Location` still points at nothing.** `POST /catalog/lookups` answers
`Location: /api/v1/catalog/lookups/{id}`, and there is no `GET` behind it. A lookup is a staff draft
that is used once, by the catalogue request, and no use case reads one back. **It stays a named debt,
not a route invented to make a header true.**

**No authorization.** The scan exposes label codes and weights to anyone who can reach the service,
which is the same hole episode 21 named. **Episode 40**, and the separate groups are what make that
one line per group.

**No caching.** A copy going out on rent changes this page. That's the same reason episode 34
gave for the listing.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green: five new facts in `SetDetailTests`, five in `ScanLabelTests`, one each in `CatalogueSetTests` and `RegisterCopiesTests`, and every episode 34–36 test untouched |
| Step 2's test against step 1's code | `Expected: NotFound, Actual: InternalServerError` |
| Step 3's "intuitive" query | `Expected: 2, Actual: 1` |
| Step 5's test against step 4's code | `Expected: [kept], Actual: [retired, kept]` |
| Step 7's test against the old header | `Expected: OK, Actual: NotFound` |
| Step 8's test against the old header | `Expected: OK, Actual: MethodNotAllowed` |
| Step 10's test against step 9's code | `Expected: BadRequest, Actual: InternalServerError` |
| `GET /catalog/sets/{id}`, a set with a copy on rent | That copy listed with `"available": false` |
| `GET /catalog/sets/{id}`, a set with no copies | `"copies": []`, `"startingPrice": null` |
| `GET /catalog/sets/{id}`, an unknown id | `404`, `"title": "No such set"` |
| `GET /catalog/copies/by-label/{label}`, in lowercase | The copy, its set, `"status": "Available"` |
| `GET /catalog/copies/by-label/BRK-0OOOOO` | `400`, with a `code` error |
| A catalogue request's `Location`, followed by hand | The set detail |
| `/openapi/v1.json` | Both new operations, their descriptions and their error responses |

Two rows are worth running on camera, one from each half. **Step 3's intuitive query**, because it looks
right and removes a feature. **Step 7's red**, because it was a bug for sixteen episodes and a
route that did not exist was hiding it.

## Next

[Episode 38 — Uploading photographs](catalog-api.md#episode-38--uploading-photographs): Azurite in
Compose, the staff upload endpoint, and why uploads go through the API rather than direct to blob.
It builds the photographs that episode 39 will add to this page, published ones only.
