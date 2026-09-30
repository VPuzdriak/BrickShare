# Episode 35 — How many sets behind each theme

← [Course plan](catalog-api.md) · Previous: [Episode 34 — Browse and filter](episode-34.md)

Episode 34 finished with a customer who can narrow the catalog five ways, and a filter list that
says nothing about what is behind each option. UC-7.2 now asks for one more thing:

> Next to each theme, the customer sees **how many sets choosing it would show**, given everything
> else they have already narrowed by. With a price limit set, a theme counts only its sets within
> that limit. A theme with nothing left still appears, with **zero**, so the list does not shift
> under the customer as they change the other filters.

That is a **facet count**, and it has one rule that the obvious implementation gets wrong: **a
count applies every filter except its own.** If the customer is looking at Icons, the Friends count
still has to say how many Friends sets they would get by switching. A count that also applied
"Icons only" would show zero next to every other theme, and there would be no way across.

**Done when** `GET /catalog/sets` returns a `themes` block beside the results. Every theme with a
set is listed with its count. The counts obey every filter except the theme, and they count the
catalog, not the page.

> **Runtime: about 12 minutes.** Seven steps, and the only new production code is one query and one
> extracted method. The time goes into the two red tests and the argument in step 5.

## Before recording

- Episode 34 merged: the `catalog_set_listings` view exists, and `BrowseSetsTests` and
  `ThemeListTests` are green.
- `docker compose up` for Postgres. There is **no migration** this episode. The view already has
  `theme_id`.
- A branch.
- `docs/IDEA.md` open at UC-7.2, and `docs/architecture/catalog.md` open at *"Search — and why
  nothing extra is needed"*. Both are quoted on screen.

**Two of the tests pass the moment they are written, and the script says so where they appear.**
They describe rules rather than drive the design, and they stay in the suite because the next
filter, and episode 36's paging, are exactly the changes that could break them. **Step 6 is not
driven by a test.** It is an argument. Step 7 is metadata and a request collection.

Every sample names its file and where in it the code goes. Where a file that already exists is
edited, the **first block is what is already there** (the anchor to find on screen) and the
**second block is what to paste**.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/IntegrationTests/Browse/BrowseSetsTests.cs` | Four facts, one theory, one helper |
| `src/…/Api/Endpoints/BrowseEndpoints.cs` | A response record, one query, one extracted method, a description |
| `src/…/Api/BrickShare.Catalog.Api.http` | Three requests appended |

Nothing in `Domain/` and nothing in `Persistence/`. **The view from episode 34 already holds every
number this episode needs; the work is asking it the right question.**

---

## Step 1 — Red → green: every theme says how many

Same shelf as episode 34, step 6:

| | Pieces | Age | Theme | Copies | Starting price |
| --- | --- | --- | --- | --- | --- |
| Concorde `10318-1` | 2083 | 18 | Icons | one Fair | **13.75** |
| Main Street Building `41704-1` | 1682 | 8 | Friends | none | **null** |
| Titanic `10294-1` | 9092 | 18 | Icons | one New | **60.00** |

With no filters, Friends holds one set and Icons holds two:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — the anchor, already there
    private async Task StockTheShelfAsync(HttpClient client)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Fact]
    public async Task Every_theme_on_the_shelf_says_how_many_sets_it_holds()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal([("Friends", 1), ("Icons", 2)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }

```

Red, and it is a build error: `BrowseSetsResponse` has no `Themes`, and there is no type with a
`SetCount`. That is a legitimate red. The test has named the shape before the shape exists.

**Caveman version:** test ask page "how many in each pile?" Page not have piles. Compiler say no.
Good. Now we know exact shape to build.

The comparison is on `(Name, SetCount)` pairs, in order. That is two rules in one line: the counts
are right, and the list is sorted by name like episode 34's filter list.

Green. First the shape, next to the records that are already there:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, already there
public sealed record BrowseSetsResponse(IReadOnlyList<SetListingResponse> Sets);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
public sealed record BrowseSetsResponse(
    IReadOnlyList<SetListingResponse> Sets,
    IReadOnlyList<ThemeFacetResponse> Themes);

public sealed record ThemeFacetResponse(Guid Id, string Name, int SetCount);
```

Then the query, at the end of `BrowseSetsAsync`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, the last line of BrowseSetsAsync
        return TypedResults.Ok(new BrowseSetsResponse(sets));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        List<ThemeFacetResponse> themes = await database.Themes
            .Where(theme => database.Sets.Any(set => set.ThemeId == theme.Id))
            .OrderBy(theme => theme.Name)
            .Select(theme => new ThemeFacetResponse(
                theme.Id,
                theme.Name,
                listings.Count(listing => listing.ThemeId == theme.Id)))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new BrowseSetsResponse(sets, themes));
```

Green.

**Caveman version:** take every theme that has a set. For each one, count sets in the filtered pile
that belong to it. Sort by name. Put the counts next to the page.

Three things in those nine lines worth a sentence each:

- **The first two lines are episode 34's `ListThemesAsync`, word for word.** The themes offered
  are the same themes that endpoint lists: every theme with at least one catalogued set. The
  counts are what's new.
- **`listings` is the filtered query, not a list.** Nothing has run yet. EF inlines the whole
  filter chain into the count, so this is one SQL statement. It is not one round trip per theme.
- **It is a second query.** The page and the counts are two statements, run one after the other.
  One query per browse request became two, and at a few thousand rows that costs nothing. It is
  still a cost, so it gets said out loud.

## Step 2 — A theme with nothing left stays, with zero

This is a rule test, and **it passes as soon as it is written.** Say so on camera.

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Fact]
    public async Task A_theme_with_nothing_left_stays_in_the_list_with_zero()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            "/api/v1/catalog/sets?maxPrice=20", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal([("Friends", 0), ("Icons", 1)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }

```

Under a price cap of 20, only Concorde qualifies. Main Street Building has no copies and so no
starting price, which episode 34 decided never matches a `maxPrice`. Friends has nothing left, and
it is **still in the list**.

It passes because the query starts from themes and *counts into* the listing. A theme with no
matches counts to zero rather than disappearing. Starting from the listing and grouping by theme
would have lost it. The test pins the choice so a later "simplification" cannot quietly flip it.

**Caveman version:** pile empty, still show pile, write zero on it. Pile not vanish when customer
move price slider.

### This relaxes episode 34, on purpose

Episode 34's filter list had a rule: *a filter option that returns nothing is a bug.* This step
lists an option that returns nothing. Both are right, because they are about different lists:

- `GET /catalog/themes` has **no count**. An option there that led to nothing would be a promise
  the list could not explain. That rule stands, and the endpoint does not change.
- The `themes` block here **carries the count**. *Friends (0)* explains itself. The alternative,
  dropping Friends whenever the price cap excludes it, means the list reshuffles every time the
  customer moves a filter. The option they were about to click moves or vanishes.

That is a judgement call rather than a law. Plenty of shops hide zero options, and a customer
seeing ten zeros is its own kind of noise. With a handful of themes, a stable list wins. With
hundreds, hiding zeros would earn its place.

## Step 3 — Red: the count that locks the customer in

Now the real test. The customer has chosen Icons, and capped the size at 2,083 pieces:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Fact]
    public async Task A_theme_count_obeys_every_filter_except_the_theme()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);
        Guid icons = await ThemeIdAsync(client, "Icons");

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?themeId={icons}&maxPieces=2083", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal(["10318-1"], page.Sets.Select(set => set.SetNumber));

        // Friends has one set under 2,083 pieces. The customer is looking at Icons, and still has
        // to be told that, or they could never find their way across.
        Assert.Equal([("Friends", 1), ("Icons", 1)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }

```

The theme id comes from the facet itself, the way a client would get it:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — the anchor, already there
    private async Task<SetListingResponse> GetAvailableSetsAsync(HttpClient client)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above GetAvailableSetsAsync
    private async Task<Guid> ThemeIdAsync(HttpClient client, string name)
    {
        BrowseSetsResponse? page =
            await client.GetFromJsonAsync<BrowseSetsResponse>("/api/v1/catalog/sets", Database.Api.Json);

        Assert.NotNull(page);

        return page.Themes.Single(theme => theme.Name == name).Id;
    }

```

Red:

```text
Assert.Equal() Failure: Collections differ
Expected: [("Friends", 1), ("Icons", 1)]
Actual:   [("Friends", 0), ("Icons", 1)]
```

The page is right: Concorde and nothing else. The Icons count is right. **Friends says 0**, and
there is a Friends set under 2,083 pieces. Step 1's query counted into `listings`, and by that
line `listings` already had `theme_id = Icons` in it. Every theme except the chosen one will
always read 0.

**Caveman version:** customer stand in Icons cave. Ask "how many in Friends cave?" Code look only
inside Icons cave. Say zero. Customer think Friends cave empty. Customer never leave. Bad.

This is the bug the episode exists for, and it is worth noticing why it happened. Nothing was
careless. Step 1 reused the filtered query because reusing it is correct for every filter *except*
one. A facet count needs **two** filtered queries that differ by exactly one filter.

## Step 4 — Green, then refactor: one filter chain, two uses

The smallest green is a reorder. Find the theme filter, currently the first `if` in
`BrowseSetsAsync`, and delete it:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, delete these lines
        if (query.ThemeId is { } themeId)
        {
            listings = listings.Where(listing => listing.ThemeId == themeId);
        }

```

Then put it back **after** the last filter, with the query captured just before it:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, the last filter
        if (query.AvailableNow is true)
        {
            listings = listings.Where(listing => listing.AvailableCount > 0);
        }
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly below it
        IQueryable<CatalogSetListing> everyFilterButTheme = listings;

        if (query.ThemeId is { } themeId)
        {
            listings = listings.Where(listing => listing.ThemeId == themeId);
        }
```

And count into the captured query:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in the themes query
                listings.Count(listing => listing.ThemeId == theme.Id)))
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
                everyFilterButTheme.Count(listing => listing.ThemeId == theme.Id)))
```

Green, all of it.

**Caveman version:** put all sieve except theme sieve. Take photo of pile. Then add theme sieve.
Page use pile with theme sieve. Counts use photo. Two piles, one sieve apart.

### Refactor: make the rule structural

It works, and it works **because of the order of the lines.** Someone who adds a sixth filter
below the theme `if` gets a filter that narrows the page and not the counts. No test catches it
until someone writes one for that filter. A rule that depends on where in a method a line gets
pasted is going to break.

So the filters move into a method whose **name** is the rule. Replace the whole of
`BrowseSetsAsync` with this, plus the new method below it:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — replaces BrowseSetsAsync entirely
    private static async Task<Ok<BrowseSetsResponse>> BrowseSetsAsync(
        [AsParameters] BrowseQuery query,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        IQueryable<CatalogSetListing> everyFilterButTheme = WhereEveryFilterButTheme(database.Listings, query);

        IQueryable<CatalogSetListing> listings = query.ThemeId is { } themeId
            ? everyFilterButTheme.Where(listing => listing.ThemeId == themeId)
            : everyFilterButTheme;

        List<SetListingResponse> sets = await listings
            .OrderBy(listing => listing.Name)
            .ThenBy(listing => listing.Id)
            .Take(query.Limit ?? BrowseQuery.DefaultLimit)
            .Select(listing => new SetListingResponse(
                listing.Id,
                listing.SetNumber,
                listing.Name,
                listing.ThemeName,
                listing.Year,
                listing.PieceCount,
                listing.MinimumAge,
                listing.MinimumRentalDays,
                listing.AvailableCount,
                listing.StartingPrice))
            .ToListAsync(cancellationToken);

        // A theme's count answers "what would I get if I chose this?", so it must not apply the
        // theme already chosen. Every theme with a set is listed, including the ones counting 0.
        List<ThemeFacetResponse> themes = await database.Themes
            .Where(theme => database.Sets.Any(set => set.ThemeId == theme.Id))
            .OrderBy(theme => theme.Name)
            .Select(theme => new ThemeFacetResponse(
                theme.Id,
                theme.Name,
                everyFilterButTheme.Count(listing => listing.ThemeId == theme.Id)))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new BrowseSetsResponse(sets, themes));
    }

    /// <summary>
    /// Every filter in <see cref="BrowseQuery"/> except the theme. The page and the theme counts
    /// both start here, so a filter added later narrows both or neither.
    /// </summary>
    private static IQueryable<CatalogSetListing> WhereEveryFilterButTheme(
        IQueryable<CatalogSetListing> listings,
        BrowseQuery query)
    {
        if (query.MinPieces is { } minPieces)
        {
            listings = listings.Where(listing => listing.PieceCount >= minPieces);
        }

        if (query.MaxPieces is { } maxPieces)
        {
            listings = listings.Where(listing => listing.PieceCount <= maxPieces);
        }

        if (query.Age is { } age)
        {
            listings = listings.Where(listing => listing.MinimumAge <= age);
        }

        if (query.MaxPrice is { } maxPrice)
        {
            // A null starting price never compares true, so this also keeps only sets with a
            // copy available. Intended: a price nobody can act on is not a price.
            listings = listings.Where(listing => listing.StartingPrice <= maxPrice);
        }

        if (query.AvailableNow is true)
        {
            listings = listings.Where(listing => listing.AvailableCount > 0);
        }

        return listings;
    }
```

Still green. Nothing about the behaviour changed. What changed is where a mistake can go:

- **A new filter goes into `WhereEveryFilterButTheme`**, and there is nowhere else for it to go.
  It narrows both, or neither. Episode 36's search predicate is the first to arrive, and it lands
  there without anyone having to remember this episode.
- **The theme filter is the only filter outside that method**, and it sits one line from the
  variable that shows why.

**Why not a `bool applyTheme` parameter?** It is the more common refactor, and it does the same
job. It hides the rule in a call site, though: `ApplyFilters(listings, query, false)` explains
nothing, and the two calls would differ by one literal. A method that *leaves one filter out by
name*, plus a visible `Where` for that one filter, puts the rule where someone reading the code
will see it. It's a close call. Either version passes the tests.

**Caveman version:** sieve rule not live in line order anymore. Sieve rule live in name of box.
New sieve go in box. Box feed both piles. Cannot forget one.

What Postgres gets for the counts, from `ToQueryString()` with `maxPieces` set:

```sql
SELECT t.id AS "Id", t.name AS "Name", (
    SELECT count(*)::int
    FROM catalog_set_listings AS c0
    WHERE c0.piece_count <= @maxPieces AND c0.theme_id = t.id) AS "C"
FROM themes AS t
WHERE EXISTS (
    SELECT 1
    FROM catalog_sets AS c
    WHERE c.theme_id = t.id)
ORDER BY t.name
```

**One count per theme, over the same view the page reads.** It is a correlated subquery, not a
`group by`. That is how "every theme, including the ones counting zero" comes out as SQL. With a
few dozen themes and a few thousand sets it is nothing. If the catalog ever grew past that, the
escape route is episode 34's: a maintained per-set summary. That is when this query would change,
and it is not something to build today.

## Step 5 — The two rules that keep the counts honest

Two more tests, and **both pass as soon as they are written.** They describe rules and don't
drive the design. They are here for the next person who changes this handler.

**The count is the number you get by clicking.** For each theme, the count must equal the length
of the results for that theme under the same other filters. That's the definition of the feature,
so it's a theory over several filter combinations:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Theory]
    [InlineData("")]
    [InlineData("&maxPrice=20")]
    [InlineData("&age=8")]
    [InlineData("&availableNow=true&maxPieces=5000")]
    public async Task A_theme_count_is_the_number_of_sets_choosing_that_theme_returns(string otherFilters)
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? counted = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?limit=50{otherFilters}", Database.Api.Json);

        Assert.NotNull(counted);

        foreach (ThemeFacetResponse theme in counted.Themes)
        {
            BrowseSetsResponse? chosen = await client.GetFromJsonAsync<BrowseSetsResponse>(
                $"/api/v1/catalog/sets?limit=50&themeId={theme.Id}{otherFilters}", Database.Api.Json);

            Assert.NotNull(chosen);
            Assert.Equal(theme.SetCount, chosen.Sets.Count);
        }
    }

```

The test never names a number. It holds the count and the listing to each other, so it keeps
holding whatever the shelf contains. The `limit=50` is there so the comparison is against the
whole list rather than the first 24.

**The counts are of the catalog, not of the page.** This one is aimed straight at episode 36:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Fact]
    public async Task The_counts_are_of_the_catalog_not_of_the_page()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            "/api/v1/catalog/sets?limit=1", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Single(page.Sets);
        Assert.Equal([("Friends", 1), ("Icons", 2)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }

```

One set on the page, and Icons still says 2. `Take` is applied to `listings`, after
`everyFilterButTheme` was captured, so it cannot reach the counts. Next episode, the keyset cursor
arrives. It will be tempting to put the cursor's `Where` into the shared filter method, because it
*looks* like a filter. This test goes red the moment someone does.

**Caveman version:** test one: number on pile must match what you get when you pick pile. Test
two: page show one rock, pile still say how many in whole cave. Page small, cave not small.

## Step 6 — The filters that get no count, and the service that is not here

**Not driven by a test.** This step is an argument.

**Why only themes.** A count needs something to hang on. A theme is a category: every set is in
exactly one, so *Icons (2)* is a complete sentence. The other filters are shaped differently:

| Filter | Shape | What a count would need |
| --- | --- | --- |
| Piece count | `minPieces` / `maxPieces`, any range | Fixed bands (*under 500, 500–1,500, …*) |
| Price | `maxPrice`, any ceiling | Fixed bands, and they move all day, because the starting price changes when a copy goes out |
| Age | `age`, the builder's age | A pick list of the ratings that exist, instead of a number |
| Available now | on or off | Nothing. It could have a count tomorrow |

For the first three, the question is not how to count. It is **whether the customer gets bands
instead of a free range**, and that is a product decision about how people shop for LEGO. Nobody
has made it, so this episode does not make it by accident. *Available now* could carry a count
cheaply. It is left out because nobody asked for it, and the facet block has room for it when
someone does.

**Why this still needs no search service.** `docs/architecture/catalog.md` says a search service
earns its place with *"faceting over large corpora"*. This episode is faceting, so the question is
fair. The answer is in the two words after it. This is **one** facet over **a few thousand** rows,
answered by one statement against a view that already exists. What would change the answer: many
facets, counted together, over a catalog large enough that a count per request stops being cheap.
BrickShare has neither, and a second data store to keep in sync would cost more than every count
it could ever save.

**Caveman version:** only count things that are piles. Price not pile, price is slope. To make
slope into piles, shop must decide where to cut. Shop not decide yet. And one small count in own
cave not need big search machine from far away.

## Step 7 — Describe it, and run it

**Not driven by a test.** OpenAPI metadata and a request collection.

The endpoint's description is episode 32's contract, and it now has a new field to explain:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, already there
            .WithDescription(
                "Sets by name, filtered by theme, piece count, age, price and availability. "
                + "startingPrice is the cheapest copy available now, and null when none is. "
                + "maxPrice therefore returns only sets with a copy available.")
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
            .WithDescription(
                "Sets by name, filtered by theme, piece count, age, price and availability. "
                + "startingPrice is the cheapest copy available now, and null when none is. "
                + "maxPrice therefore returns only sets with a copy available. "
                + "themes lists every theme with a set, each with the number of sets choosing it "
                + "would return under the other filters, including 0.")
```

**A new field on a response is an additive change.** No client that reads `sets` breaks, which is
why the field goes on this route and no second version of it is needed.

**Caveman version:** map of API say what new number mean. Old visitor not hurt, new number just
extra.

Then the requests, appended after episode 34's browse requests at the end of
`BrickShare.Catalog.Api.http`:

```http
### BrickShare.Catalog.Api.http — append at the end of the file

### No filters. Look at "themes": every theme with a set, and how many it holds.
GET {{host}}/api/v1/catalog/sets

### Pick a theme. The other themes keep their counts, so the customer can switch.
GET {{host}}/api/v1/catalog/sets?themeId={{themeId}}

### Under 20. Themes with nothing that cheap stay in the list, with 0.
GET {{host}}/api/v1/catalog/sets?maxPrice=20
```

Run them. The shot to hold on is the **second** request: one theme's sets on the page, and every
other theme still counted beside them.

---

## What this episode is not

**No counts on piece count, price, age or availability.** Step 6 says why: bands are a product
decision, not a query.

**No change to `GET /catalog/themes`.** It stays the unfiltered filter list, with no counts and no
empty themes. The overlap is real: a client building a results page reads `themes` from
`/catalog/sets` and never needs the other endpoint. That endpoint stays for anything that wants
the list without running a search.

**No search, no page two.** Episode 36, which inherits two rules from this one: search goes into
`WhereEveryFilterButTheme`, and the cursor does not.

**No caching, no summary table.** The same reasons as episode 34. Nothing is slow, so there is
nothing to earn.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green: four new facts and a four-row theory in `BrowseSetsTests`, and every episode 34 test untouched |
| Step 3's test against step 1's code | `Friends` expected 1, actual 0 |
| `dotnet ef migrations has-pending-model-changes` | No changes. Nothing in `Persistence/` moved |
| `GET /catalog/sets` | A `themes` array, sorted by name, each entry with `id`, `name`, `setCount` |
| `GET /catalog/sets?themeId={icons}&maxPieces=2083` | One set on the page; Friends 1, Icons 1 |
| `GET /catalog/sets?maxPrice=20` | Friends listed with `setCount: 0` |
| `GET /catalog/sets?limit=1` | One set on the page; Icons still 2 |
| `/openapi/v1.json` | `ThemeFacetResponse` in the schemas, and the new description on the browse operation |

The row to run on camera is the third one, with step 1's code. **The failure is the lesson:** a
count that is right for the theme you are looking at and wrong for every other.

## Next

[Episode 36 — Search, and page two](episode-36.md): *by name or
set number*. `pg_trgm` so that *"Titanc"* finds the Titanic, keyset paging over the order
episode 34 made total, and the first two tests of this episode's rules: search narrows the counts,
the cursor does not.
