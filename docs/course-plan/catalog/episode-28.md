# Episode 28 — The request body that loses its facts

← [Course plan](catalog-api.md) · Previous: [Episode 27 — The lookup that owns the facts](episode-27.md)

Episode 27 built the lookup and fixed nothing. That was deliberate and it was said on camera, and
it is still true in `main` right now: `POST /catalog/sets` accepts nine fields, five of them are
LEGO's facts rather than the shop's, and the four-piece Titanic from episode 27's opening `curl`
still comes back `201 Created`.

This episode is the other half. Create is rewritten to take a `lookupId` and the four values staff
are actually entitled to decide, **the other five fields are deleted from the type**, and the
vulnerability stops being expressible rather than stopping being allowed.

That distinction is the episode. It is an authorization fix, not an API design preference.

**Done when** `POST /catalog/sets` takes five fields, the step 1 body comes back `400`, a set
catalogued through the two-call flow carries a name and a piece count the client never sent, and a
`lookupId` nobody issued comes back `422`.

> **Runtime: about 14 minutes.** The step most likely to overrun is step 5, which is four test
> edits in a row — if it does, the `UnmappedMemberHandling` close call is the one to cut.

## Before recording

- Episode 27 merged: `POST /catalog/lookups`, the `rebrickable_snapshots` table, the shared stub.
- `docker compose up` for Postgres, and `dotnet user-secrets` still holding your Rebrickable key.
- A branch.
- [`episode-23.md`](episode-23.md) open at the status-code argument. Step 4 walks that ladder again
  and arrives somewhere else, and the contrast is easier to make with the original on screen.
- **No `dotnet ef` needed.** This episode adds no migration, and step 3 says why that is a
  statement rather than an omission.

**One of the six steps is not driven by a test**: step 6 is an `.http` file, which is a request
collection and not code. Everything else here goes red first, and step 5 keeps a test that was
green the moment it was written — for a reason it states.

Every sample below names its file and where in it the code goes. Where something is edited in a
file that already exists, the **first block is what is already there** — the anchor to find on
screen — and the **second block is what to paste**. Every block is copy-paste clean: no markers,
no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/CatalogSets/CatalogueSetTests.cs` | **Rewritten** — four tests reworked, two added |
| `src/…/Api/Endpoints/CatalogSetEndpoints.cs` | The request record loses five fields; the handler reads a snapshot |
| `src/…/Api/Endpoints/CatalogueSetRequestValidator.cs` | Five rules deleted, one added |
| `src/…/Api/BrickShare.Catalog.Api.http` | The two-call flow |

Four files, and two of them are the ones episode 27 pointedly did not open. Note what is **not**
here: no domain change, no migration, no `Program.cs`, and `LookupTests.cs` untouched. A fix this
small is the payoff for having spent episode 27 building the thing it depends on.

---

## Step 1 — Three fixes that do not work

Start where episode 27 finished. The API is running; send the fake set again:

```bash
curl -i -X POST http://localhost:5080/api/v1/catalog/sets \
  -H "Content-Type: application/json" \
  -d '{ "setNumber": "99999-9", "name": "Definitely A Real Set", "theme": "Free Stuff",
        "year": 2021, "pieceCount": 4, "retailPrice": 0.01, "baseRentalPrice": 0.01,
        "minimumRentalDays": 1, "minimumAge": 0 }'
```

`201 Created`. Episode 27 established whose data each of those fields is, so that argument does not
need making twice. What needs making is the *shape* of the fix, because there are four candidates
and three of them are traps.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — near the bottom
/// <summary>
/// What staff send to catalogue a set. Every product fact in here is client-supplied, which is a security problem
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
```

| Fix | Why it is not enough |
| --- | --- |
| **Check it** — compare `request.Name` against the snapshot and refuse a mismatch | Correct today, and one `if` away from wrong forever. The client's value is still in the type, so the check is the only thing standing between it and the database, and a check is a line somebody can delete, reorder, or write a second code path around |
| **Default it** — treat an absent `Name` as "take it from the snapshot" | A *sent* `Name` still wins. This is not a fix at all, it is a fix for the client that did not attack you |
| **Overwrite it** — assign the snapshot's value over whatever arrived | Works, and reads as an accident. A field that is written and then immediately overwritten is the exact shape a future refactor "tidies up" |
| **Delete it** | Nowhere for the value to land, no line to remove, nothing to regress |

The first three all leave a code path where the client's value can win. Only the fourth removes the
category of bug, and that is the general rule worth carrying out of this episode:

> **A field the client must not choose must not be a field the client can send.**

Not checked. Not defaulted. Not overwritten. Absent.

---

## Step 2 — Red: two calls where there was one

The test side changes first, and it changes in a way that costs something.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — as it is
    [Fact]
    public async Task A_catalogued_set_comes_back_created()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — replace it
    [Fact]
    public async Task A_catalogued_set_comes_back_created()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid lookupId = await LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
```

`ValidRequest` shrinks to the four fields the shop owns, and the helper that does the first call
goes in beside it:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — as it is, bottom of the class
    private static object ValidRequest(int minimumRentalDays = 7) => new
    {
        setNumber = "10294-1",
        name = "Titanic",
        theme = "Icons",
        year = 2021,
        pieceCount = 9092,
        retailPrice = 629.99m,
        baseRentalPrice = 60.00m,
        minimumRentalDays,
        minimumAge = 18
    };
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — replace it
    private static object ValidRequest(Guid lookupId, int minimumRentalDays = 7) => new
    {
        lookupId,
        retailPrice = 629.99m,
        baseRentalPrice = 60.00m,
        minimumRentalDays,
        minimumAge = 18
    };

    /// <summary>
    /// The first half of the two-call flow. Every create test needs one, because a lookupId is the
    /// only way left to name a set and only episode 27's endpoint issues one.
    /// </summary>
    private async Task<Guid> LookUpTitanicAsync(HttpClient client)
    {
        Database.Rebrickable.Sets["10294-1"] = new
        {
            set_num = "10294-1",
            name = "Titanic",
            year = 2021,
            theme_id = 252,
            num_parts = 9092,
            set_img_url = "https://cdn.rebrickable.com/media/sets/10294-1.jpg"
        };

        Database.Rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)null };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        response.EnsureSuccessStatusCode();

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        return draft.LookupId;
    }
```

One new `using`, for `LookupResponse`:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — add to the top
using BrickShare.Catalog.Api.Endpoints;
```

Run it. **Red, and read the failure before fixing it:**

```
Expected: Created
Actual:   BadRequest
```

Not a compile error this time — the body is an anonymous object, so it compiles fine. The `400`
comes from the validator, which still insists on a `setNumber` and a `name` that the test no longer
sends. The test is a statement about the request shape, and the old shape is refusing it.

**Say the cost out loud.** Every create test is now two HTTP requests and needs the Rebrickable stub
arranged, in a test file that until today needed neither. That is what making create depend on a
lookup costs, it is charged to every future test in this file, and it is worth it — but pretending
the dependency is free would be the wrong lesson.

The payload duplication is a genuine close call, and it is small enough to name and move on:
`LookupTests.cs` has its own `Titanic()` and `Icons()`. Two copies, two files, and they are copies
for a reason — `LookupTests` asserts *on* those values, so a shared builder would let a change in
one test silently rewrite the other's expectations. If a third file needs them, extract then.

---

## Step 3 — Green: the five fields leave

The record first. This is the whole fix, and it is a deletion.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replace CatalogueSetRequest
/// <summary>
/// What staff send to catalogue a set: a lookup to take the product facts from, and the four
/// commercial decisions that are the shop's to make. There is no field here for a name, a theme, a
/// year or a piece count, and that absence is the security control — see episode 28.
/// </summary>
public sealed record CatalogueSetRequest(
    Guid LookupId,
    decimal RetailPrice,
    decimal BaseRentalPrice,
    int MinimumRentalDays,
    int MinimumAge);
```

The comment that said "which is a security problem" is gone with it, because the problem is gone.
Leaving it would be worse than never having written it.

The validator loses the five rules that had nothing left to validate:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogueSetRequestValidator.cs — replace the constructor body
        // Guid.Empty, and only that. Anything that is not a Guid at all never reaches a rule:
        // JSON binding refuses it first, with a 400 of the framework's own.
        RuleFor(request => request.LookupId).NotEmpty()
            .WithMessage("A lookupId is required. Look the set up first: POST /api/v1/catalog/lookups.");

        RuleFor(request => request.RetailPrice).GreaterThanOrEqualTo(0m)
            .WithMessage("A retail price cannot be negative.");

        RuleFor(request => request.BaseRentalPrice).GreaterThanOrEqualTo(0m)
            .WithMessage("A base rental price cannot be negative.");

        RuleFor(request => request.MinimumRentalDays).GreaterThanOrEqualTo(1)
            .WithMessage("A rental lasts at least one day.");

        RuleFor(request => request.MinimumAge).InclusiveBetween(0, 18)
            .WithMessage("An age rating is between 0 and 18.");
```

Nine rules to five, and the four that survived are the four that were always the shop's to check.
The `SetNumber` delegation from episode 22 is not deleted so much as **relocated** — it still runs,
in `LookupRequestValidator`, one endpoint earlier, on the only request that still carries a set
number. Worth pointing at: the validation did not get weaker, it moved to the place that owns the
field.

Now the handler. It reads the facts instead of being told them:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — as it is
        CatalogSet catalogSet = CatalogSet.Catalogue(
            SetNumber.Parse(request.SetNumber),
            request.Name,
            request.Theme,
            request.Year,
            request.PieceCount,
            new Money(request.RetailPrice),
            new Money(request.BaseRentalPrice),
            request.MinimumRentalDays,
            request.MinimumAge);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replace it
        RebrickableSnapshot? snapshot =
            await database.Snapshots.FindAsync([request.LookupId], cancellationToken);

        // Step 4 decides what goes here. For now the next line throws, and that is the red.
        CatalogSet catalogSet = CatalogSet.Catalogue(
            snapshot!.Number,
            snapshot.Name,
            snapshot.ThemeName,
            snapshot.Year,
            snapshot.PieceCount,
            new Money(request.RetailPrice),
            new Money(request.BaseRentalPrice),
            request.MinimumRentalDays,
            request.MinimumAge);
```

The duplicate-set message referenced `request.SetNumber`, which no longer exists:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — as it is
            throw new DomainRuleViolationException($"Set {request.SetNumber} is already catalogued.", ex);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replace it
            throw new DomainRuleViolationException($"Set {snapshot.Number} is already catalogued.", ex);
```

And one new `using`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — add to the top
using BrickShare.Catalog.Api.Rebrickable;
```

Step 2's test is green.

**Now list what did not change, because the absences are the argument.**

- **`CatalogSet.cs` is untouched.** The domain's `Catalogue` factory takes the same nine arguments
  it took in episode 20. Who *supplies* them is an API concern, and the domain never found out that
  five of them now come from a snapshot — which is only true because episode 27 refused to put
  `RebrickableSnapshot` in the domain project.
- **No migration.** `catalog_sets` has the same columns and `rebrickable_snapshots` has the same
  columns. The entire fix is in a request record, which has no database representation at all. If
  `dotnet ef migrations add` would produce anything here, something has gone wrong.
- **`Program.cs` is untouched.** `IValidator<CatalogueSetRequest>` is registered against the same
  type name; the type just has fewer fields in it.
- **`CatalogSetResponse` keeps all nine fields.** A request body and a response body are not the
  same kind of object. The request is a set of *instructions*, and the client is not entitled to
  give five of them. The response is a *description of a resource the shop now owns*, and every
  fact in it is fair to publish.

---

## Step 4 — Red, then green: the same ladder, a different answer

There is a new way to fail, and step 3 left it as a `!`. A `lookupId` that was never issued.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs
// after A_catalogued_set_comes_back_created
    [Fact]
    public async Task A_lookup_that_was_never_issued_cannot_be_catalogued()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("lookups", problem.Detail);
    }
```

```
Expected: UnprocessableEntity
Actual:   InternalServerError
```

A `500`, from the `NullReferenceException` the `!` promised. Episode 23's diagnosis applies word for
word: the server did what it was told and had no way to say so. So walk episode 23's ladder, on
camera, and notice it does not land where it landed last time.

| Code | The claim it makes | Here |
| --- | --- | --- |
| `404` | The resource you addressed does not exist | **No.** The client addressed `/api/v1/catalog/sets`, which exists and accepts posts. A `404` on a POST to a live collection reads as a wrong URL and sends a developer to check their routing |
| `409` | Your request conflicts with the current state of the resource | **No.** Episode 23's and 24's code, and it needs something to conflict *with*. Nothing exists here at all |
| `400` | Your request is malformed | **The honest runner-up.** Reachable with `MustAsync` and an injected `CatalogDbContext`, which puts the error in episode 22's field-by-field shape alongside `retailPrice`. Costs a database read inside a validator, a second read in the handler that needs the row anyway, and a window between the two where the row could vanish — a check that is not the read is a race |
| `422` | I understood your request and cannot process it | **Ships.** The body parsed, every field is well-formed, every rule passed, and the one thing it references is not there |

That is the contrast to make explicitly. Episode 27 asked this exact question about an unknown *set
number* and answered `404`, because the thing the client asked about — the set — genuinely did not
exist. One endpoint over, the same shape of failure is a `422`, because what the client addressed
does exist and what it *referenced* does not.

> **A status code is a claim about who has to do something next.** Episode 23 said it about `409`.
> The claims are different here, so the code is too.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — as it is
        // Step 4 decides what goes here. For now the next line throws, and that is the red.
        CatalogSet catalogSet = CatalogSet.Catalogue(
            snapshot!.Number,
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replace those lines
        if (snapshot is null)
        {
            // Not a 404: /catalog/sets exists. Not a 409: there is no state to conflict with. The
            // request was understood in full and cannot be carried out. See episode 28, step 4.
            return TypedResults.Problem(
                title: "No such lookup",
                detail: $"Lookup {request.LookupId} does not exist. Look the set up first at /api/v1/catalog/lookups.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        CatalogSet catalogSet = CatalogSet.Catalogue(
            snapshot.Number,
```

The handler's return type grows the third case, exactly as episode 27's lookup did:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — as it is
    private static async Task<Results<Created<CatalogSetResponse>, ValidationProblem>> CatalogueAsync(
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replace it
    private static async Task<Results<Created<CatalogSetResponse>, ValidationProblem, ProblemHttpResult>> CatalogueAsync(
```

Green. And the `Results<>` union earning its keep is worth five seconds: the signature now lists
every response this endpoint can produce, the compiler enforces that list, and episode 32's OpenAPI
document is generated from it rather than from a comment.

---

## Step 5 — The test that proves it, and four that had to change

### The one that matters

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs
// after A_lookup_that_was_never_issued_cannot_be_catalogued
    [Fact]
    public async Task A_client_cannot_invent_a_product()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid lookupId = await LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets", new
        {
            lookupId,
            retailPrice = 0.01m,
            baseRentalPrice = 0.01m,
            minimumRentalDays = 1,
            minimumAge = 0,

            // Episode 27's four-piece Titanic, sent anyway, by a client that has read the old docs.
            setNumber = "99999-9",
            name = "Definitely A Real Set",
            theme = "Free Stuff",
            year = 2021,
            pieceCount = 4
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        CatalogSetResponse? created = await response.Content.ReadFromJsonAsync<CatalogSetResponse>();

        Assert.NotNull(created);

        // Rebrickable's, every one of them, and the client sent something else for all four.
        Assert.Equal("10294-1", created.SetNumber);
        Assert.Equal("Titanic", created.Name);
        Assert.Equal("Icons", created.Theme);
        Assert.Equal(9092, created.PieceCount);

        // The shop's four did land, because those the client is entitled to choose.
        Assert.Equal(0.01m, created.RetailPrice);
        Assert.Equal(0, created.MinimumAge);
    }
```

**Green the moment it is written.** `CLAUDE.md` is explicit that this is fine and should be said
rather than hidden: some tests drive a design and some describe a rule. Step 2's test drove this
design; this one pins the property the design exists for, and it is the single test to point at when
somebody asks what episode 28 was about.

Look at *why* it passes. `System.Text.Json` ignores a member with no matching property, so those
five values are read off the wire and dropped. Nothing compared them to anything. Nothing overwrote
them. **They lost by construction**, which is the difference between a rule and a control.

**The close call:** `JsonSerializerOptions.UnmappedMemberHandling.Disallow` would turn that body
into a `400` telling staff exactly which five fields are no longer theirs, which is genuinely
kinder than silently ignoring them. It is not taken, for two reasons. It is a global serializer
setting, so it changes the contract of every endpoint in the service to fix one endpoint's error
message; and the security property does not depend on it either way — ignoring an unmapped member
is already safe, and a stricter setting would make the API louder, not safer. It goes on the list
for episode 32, where the API's self-description is the subject.

### The three that had to move

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — as it is
        Assert.NotNull(problem);
        Assert.Contains("setNumber", problem.Errors.Keys);
        Assert.Contains("name", problem.Errors.Keys);
        Assert.Contains("minimumRentalDays", problem.Errors.Keys);
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — replace it
        Assert.NotNull(problem);
        Assert.Contains("lookupId", problem.Errors.Keys);
        Assert.Contains("minimumRentalDays", problem.Errors.Keys);

        // Yesterday these two were the first assertions in this test. A field that cannot be
        // rejected, because it cannot be sent, is the entire point of the episode.
        Assert.DoesNotContain("setNumber", problem.Errors.Keys);
        Assert.DoesNotContain("name", problem.Errors.Keys);
```

**Two inverted assertions are the fix, expressed as a test.** That is the thirty seconds to spend
here: the diff on this test is a `Contains` becoming a `DoesNotContain`, and a reader who understands
why will not need step 1's table explained again.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — as it is
    public async Task A_minimum_rental_period_the_shop_cannot_honour_is_refused()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(minimumRentalDays: 30));
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — replace it
    public async Task A_minimum_rental_period_the_shop_cannot_honour_is_refused()
    {
        HttpClient client = Database.Api.CreateClient();
        Guid lookupId = await LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId, minimumRentalDays: 30));
```

Nothing else in that test changes, and that is the reassuring result: `minimumRentalDays` was always
the shop's field, so episode 23's `409` path is not affected by any of this.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — as it is
    public async Task A_set_that_is_already_catalogued_cannot_be_catalogued_again()
    {
        HttpClient client = Database.Api.CreateClient();

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest());
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/CatalogSets/CatalogueSetTests.cs — replace it
    public async Task A_set_that_is_already_catalogued_cannot_be_catalogued_again()
    {
        HttpClient client = Database.Api.CreateClient();

        // One lookup, quoted twice. Episode 27 decided a snapshot has no single-use flag, and this
        // is that decision being exercised rather than described.
        Guid lookupId = await LookUpTitanicAsync(client);

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/v1/catalog/sets", ValidRequest(lookupId));
```

The `409` still comes from episode 24's unique index, on `catalog_sets`, not from anything about
lookups. **The two uniqueness rules are independent and both still hold**: many snapshots per set
number, one catalog set per set number.

Run the suite. Six tests in this file, all green, and `LookupTests` never opened.

---

## Step 6 — The `.http` file, and a rule that is now free

Episode 27's step 7 never made it into the repository — `BrickShare.Catalog.Api.http` still holds
nothing but the three health requests. Put the whole flow in now, in the order somebody reading it
cold would want to run it. Not test-driven, and it does not need to be: this is a request
collection, not code.

```http
### BrickShare.Catalog.Api.http — append

@lookupId = paste-the-lookupId-from-the-response-below

### 1. Look the set up. 201, and every product fact comes back without anybody typing it.
POST {{host}}/api/v1/catalog/lookups
Content-Type: application/json

{ "setNumber": "10294-1" }

### 2. Catalogue it. Five fields: which lookup, and the four decisions the shop owns.
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{ "lookupId": "{{lookupId}}", "retailPrice": 629.99, "baseRentalPrice": 60.00,
  "minimumRentalDays": 7, "minimumAge": 18 }

### 3. The same lookup again. 409 from episode 24's unique index — the set, not the lookup.
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{ "lookupId": "{{lookupId}}", "retailPrice": 629.99, "baseRentalPrice": 60.00,
  "minimumRentalDays": 7, "minimumAge": 18 }

### 4. A lookupId nobody issued. 422: understood in full, and referencing nothing.
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{ "lookupId": "01931f3c-0000-7000-8000-000000000000", "retailPrice": 629.99,
  "baseRentalPrice": 60.00, "minimumRentalDays": 7, "minimumAge": 18 }

### 5. Episode 27's fake Titanic. 400 — there is no lookupId, and the five invented facts
### are not rejected so much as unreadable.
POST {{host}}/api/v1/catalog/sets
Content-Type: application/json

{ "setNumber": "99999-9", "name": "Definitely A Real Set", "theme": "Free Stuff",
  "year": 2021, "pieceCount": 4, "retailPrice": 0.01, "baseRentalPrice": 0.01,
  "minimumRentalDays": 1, "minimumAge": 0 }

### 6. A typo, one endpoint earlier. 404 with a problem document, not a 502.
POST {{host}}/api/v1/catalog/lookups
Content-Type: application/json

{ "setNumber": "99999-9" }
```

Run 1, 2 and 5 on camera, in that order, against a real key. Request 5 is the closing shot of both
episodes: the body that got a `201 Created` at the start of step 1 now gets a `400` whose error list
mentions one field, `lookupId`, and says nothing at all about the five invented facts — because
there is nothing there to have an opinion about.

### The rule that came free

`docs/IDEA.md` has carried this line since step 1 of the whole project:

> | Missing spec data | Cataloguing a set **blocks** until the Rebrickable lookup succeeds. |

Nothing in the code enforces that, and nothing needs to. The only field that identifies a set is
one that only a successful lookup issues, so create cannot be reached without one. **The rule is
not implemented, it is true** — and an invariant guaranteed by a type's shape is a better outcome
than the same invariant guaranteed by a check, for exactly the reasons in step 1's table.

---

## What this episode is not

**No snapshot expiry, and no single-use flag.** Episode 27 deferred this decision to the episode
where create reads the row, and this is it: nothing ships. A snapshot could be a month old when
create quotes it, and Rebrickable could have corrected a piece count in the meantime. The cost of
being wrong is a set catalogued with a slightly stale piece count — a value a staff member can
correct. The cost of an expiry is a staff member losing a half-filled form to a rule nobody
explained to them. `fetched_at` stays in the table, so a shop that finds out it was the wrong call
has everything it needs to change its mind.

**No `snapshot_id` column on `catalog_sets`.** Tempting, and it would answer a real question —
*where did these facts come from?* — which is the sort of thing an auditor asks. It is refused for
episode 27's reason: a foreign key from a domain table to an infrastructure table would make
`CatalogSet` know that Rebrickable exists. If provenance becomes a requirement, it belongs in an
outbox or an audit log, not in the aggregate.

**No authorization.** Every endpoint in this service is still open to anybody who can reach it,
until **episode 38**. That is exactly why this fix had to come first: an authenticated caller who
can still invent a product is an authenticated attacker, and adding Entra ID on top of a nine-field
create body would have shipped a login screen in front of an open door.

**No stricter JSON.** See step 5's close call. The extra members are ignored, not refused.

**No `GET /catalog/sets/{id}`.** `TypedResults.Created` still points at a route that does not exist
yet. Left exactly as it is: it becomes real in **episode 35**, and inventing it here to make a
header look tidy would be building a feature to satisfy a string.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green — six tests in `CatalogueSetTests`, four untouched in `LookupTests` |
| `dotnet ef migrations list` | `AddRebrickableSnapshots` still last. **Nothing new** |
| `git diff --stat src/Catalog/BrickShare.Catalog.Domain` | Empty — the domain never found out |
| `git diff --stat src/Catalog/BrickShare.Catalog.Api/Migrations` | Empty — the fix has no database shape |
| `.http` requests 1 then 2 | `201`, then `201` with `"name": "Titanic"` |
| `.http` request 3 | `409`, and `select count(*) from catalog_sets` is `1` |
| `.http` request 4 | `422`, `application/problem+json`, a `traceId` extension |
| `.http` request 5 | `400`, one error key: `lookupId` |
| `select name, piece_count from catalog_sets` | `Titanic`, `9092` — and no client ever sent either |

The last row is the one to run on camera. Episode 27's argument was proved by an absence; this one
is proved by two values sitting in a table that no request body contained.

## Next

[Episode 29 — A copy belongs to a set](episode-29.md): the sets exist, so the boxes on the shelf
can be registered against them — one at a time, with a label code BrickShare mints itself because a
LEGO box carries no serial number, and a baseline weight recorded while the box is known complete.
Episode 30 turns that endpoint into a batch of any size in one transaction and scales validation up
to `RuleForEach` and an endpoint filter; episode 31 makes retirement a state change rather than a
delete, and episode 15's "a copy on rent cannot be retired" rule finally runs end to end over HTTP.

Two episodes, one question, and it is the thing to carry forward: **whose data is this?**, asked of
every field in every request body. A field the client should not be able to choose must not be a
field the client can send — and here it cost one extra endpoint and five deleted lines.
