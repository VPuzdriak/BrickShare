# Episode 36 — Search, and page two

← [Course plan](catalog-api.md) · Previous: [Episode 35 — How many sets behind each theme](episode-35.md)

Episode 35 finished with a results page that can explain its filter list. Two parts of UC-7.2 are
still missing, and both are things every customer tries in the first ten seconds:

> **UC-7.2 — Search and filter.** By name or set number, and by the attributes that matter when
> choosing something to build […]

A customer types *"titanc"*, because nobody spells on a phone, and expects the Titanic. Another
types *"10294"*, the number printed on the box. And a customer who scrolls past the first 24 sets
finds that nothing comes after them. Episode 34 said this plainly: `limit` caps the list and nothing
continues it.

This episode adds both, and neither needs a new Azure resource. **Search is `pg_trgm`, an extension
that ships with Postgres.** Paging is a `WHERE` clause, and episode 34 already made the order it
depends on total.

**Done when** `GET /catalog/sets?search=titanc` finds the Titanic and `?search=10294` finds it by
number. Every response carries a `next` cursor that is `null` on the last page. Following `next`
visits every set exactly once, even if a set is catalogued mid-scroll. And the theme counts obey the
search but never the cursor.

> **Runtime: about 24 minutes — over the 10–15 budget, and said so up front.** This episode has two
> subjects, search and paging, which share an endpoint and nothing else. **Record it as two**, with
> the seam marked in place after step 6:
>
> - **36a — "Search" (steps 1–6, ~13 minutes).** Four red tests that each teach one thing about
>   matching text in Postgres, the extension and its index, and the Azure setting it needs.
> - **36b — "Page two" (steps 7–12, ~11 minutes).** Keyset paging, the cursor, and the rule from
>   episode 35 that the cursor must not touch the counts.
>
> The numbering of episode 37 onwards does not move. The script reads correctly either way.

## Before recording

- Episode 35 merged. `BrowseSetsTests` is green, including the four facet tests.
- `docker compose up` for Postgres, and a few sets catalogued in it, so the `EXPLAIN` in step 4 has
  rows to look at. Also a `psql` open against it:
  `docker compose exec postgres psql -U brickshare brickshare_catalog`.
- A branch.
- `docs/IDEA.md` open at UC-7.2, and `docs/architecture/catalog.md` open at *"Search — and why
  nothing extra is needed"*. Both are quoted on screen.
- **Know the push order before you start.** Step 4 adds a Terraform setting that has to reach Azure
  *before* the migration does. The simplest way to do it is two pushes, and step 4 explains why.

**Three tests pass the moment they are written, and the script says so where they appear.** They
describe rules rather than drive the design. **Step 4 is mostly infrastructure**: the extension, the
index, the migration and a Terraform setting are not driven by a test, although a red test is what
tells us we need them. Steps 7 and 11 are arguments. Step 12 is metadata and a request collection.

Every sample names its file and where in it the code goes. Where a file that already exists is
edited, the **first block is what is already there** (the anchor to find on screen) and the
**second block is what to paste**.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `tests/…/IntegrationTests/Browse/BrowseSetsTests.cs` | One six-row theory, four facts, one row in an existing theory |
| `src/…/Api/Endpoints/BrowseQuery.cs` | Two parameters, one validator rule |
| `src/…/Api/Endpoints/BrowseCursor.cs` | **New** |
| `src/…/Api/Endpoints/BrowseEndpoints.cs` | The search predicate, the cursor, `next`, a description |
| `src/…/Api/Persistence/CatalogDbContext.cs` | One line: the extension |
| `src/…/Api/Persistence/CatalogSetConfiguration.cs` | One index |
| `src/…/Api/Migrations/…_AddSetNameSearch.cs` | Generated |
| `infra/main.tf` | One resource: the extension allow-list |
| `src/…/Api/BrickShare.Catalog.Api.http` | Four requests appended |

Nothing in `Domain/`. **Search and paging are questions about how to read the catalog, and the
domain has no opinion about either.**

---

# 36a — Search

## Step 1 — Red → green: find a set by its name

Same shelf as episodes 34 and 35:

| | Pieces | Age | Theme | Copies | Starting price |
| --- | --- | --- | --- | --- | --- |
| Concorde `10318-1` | 2083 | 18 | Icons | one Fair | **13.75** |
| Main Street Building `41704-1` | 1682 | 8 | Friends | none | **null** |
| Titanic `10294-1` | 9092 | 18 | Icons | one New | **60.00** |

The test is a theory from the start. Every step in this half of the episode is one more row in it,
and each row is one thing text matching gets wrong:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — the anchor, already there
    private async Task StockTheShelfAsync(HttpClient client)
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Theory]
    [InlineData("Titanic", new[] { "10294-1" })]
    public async Task Search_finds_sets_by_name_or_set_number(string search, string[] expectedSetNumbers)
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?search={Uri.EscapeDataString(search)}", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal(expectedSetNumbers, page.Sets.Select(set => set.SetNumber));
    }

```

Red, and this time it compiles:

```text
Assert.Equal() Failure: Collections differ
Expected: ["10294-1"]
Actual:   ["10318-1", "41704-1", "10294-1"]
```

**Minimal APIs ignore a query parameter nobody bound.** `?search=Titanic` reached the endpoint and
was dropped without a word, so the customer got the whole catalog back. That is worth a pause: a
typo in a parameter name fails the same silent way, and only a test that checks the *result* notices.

**Caveman version:** customer shout "Titanic!" Server not have ears for that word. Server shrug,
give whole cave.

Green. First, the parameter:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — the anchor, already there
/// <param name="ThemeId">An id from GET /catalog/themes.</param>
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — paste directly above it
/// <param name="Search">Part of a set's name, or the start of its set number.</param>
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — the anchor, already there
public sealed record BrowseQuery(
    Guid? ThemeId,
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — what it becomes
public sealed record BrowseQuery(
    string? Search,
    Guid? ThemeId,
```

Then the predicate. **Episode 35 already decided where it goes.** Search is a filter, so it goes
into `WhereEveryFilterButTheme`, and nowhere else:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, the first filter in WhereEveryFilterButTheme
        if (query.MinPieces is { } minPieces)
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly above it
        if (query.Search is { } search)
        {
            listings = listings.Where(listing => EF.Functions.Like(listing.Name, $"%{search}%"));
        }

```

Green. `EF.Functions.Like` is the plain SQL `LIKE`, written out so the SQL on screen is exactly what
the C# says. `%` on both sides means *anywhere in the name*.

**Caveman version:** give server ears for "search". Look for word anywhere inside name.

## Step 2 — Red → green: the collation trap

The next row is a customer who does not press Shift:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — the anchor, already there
    [InlineData("Titanic", new[] { "10294-1" })]
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly below it
    [InlineData("titanic", new[] { "10294-1" })]
```

Red:

```text
Assert.Equal() Failure: Collections differ
Expected: ["10294-1"]
Actual:   []
```

**Postgres's `LIKE` is case-sensitive.** This is the bug episode 17 predicted, word for word:
*"SQLite's `LIKE` is case-insensitive for ASCII by default; Postgres's is not. That is a test that
passes and a search endpoint that does not, and episode 36 is where it would be discovered."* Had
the integration tests run against SQLite, this row would be green, and production would have shipped
a search that cannot find *"titanic"*. This is the payoff for testing against the real database.

**Caveman version:** small rock and big rock look same to customer. To Postgres, different rock.
Fake test cave say same. Real cave say different. Real cave win.

Green, one word:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in WhereEveryFilterButTheme
            listings = listings.Where(listing => EF.Functions.Like(listing.Name, $"%{search}%"));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
            listings = listings.Where(listing => EF.Functions.ILike(listing.Name, $"%{search}%"));
```

`ILIKE` is Postgres's case-insensitive `LIKE`. It is a Postgres extension to SQL, and the method
comes from the Npgsql provider rather than from EF. There are other fixes: `lower(name)` on both
sides, a `citext` column, or a case-insensitive collation. `ILIKE` is the smallest one. It also won't
survive the next step, at least for names.

## Step 3 — Red, twice: a typo

The row this whole half of the episode is for:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly below the "titanic" row
    [InlineData("Titanc", new[] { "10294-1" })]
```

Red, and there is no clever `LIKE` pattern that fixes it. *"Titanc"* is not inside *"Titanic"*. No
substring match can find a missing letter.

What can find it is **trigram matching**. Postgres's `pg_trgm` extension cuts a string into every
three-letter run, padded at the word edges, and measures how many runs two strings share:

```text
Titanic → "  t", " ti", "tit", "ita", "tan", "ani", "nic", "ic "     (8)
Titanc  → "  t", " ti", "tit", "ita", "tan", "anc", "nc "            (7)
shared  → 5 of Titanc's 7 = 0.71
```

One wrong letter breaks two or three trigrams, and the rest still match. **That is why trigrams
tolerate typos: a mistake is local, and so is the damage.**

Npgsql translates `pg_trgm`'s operators without a plugin. Replace the `ILike`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in WhereEveryFilterButTheme
            listings = listings.Where(listing => EF.Functions.ILike(listing.Name, $"%{search}%"));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
            listings = listings.Where(listing => EF.Functions.TrigramsAreWordSimilar(search, listing.Name));
```

Run it. **Still red**, and now every row fails, including the two that passed a minute ago:

```text
System.Net.Http.HttpRequestException: Response status code does not indicate success: 500
  ---> Npgsql.PostgresException: 42883: operator does not exist: text <% text
```

`TrigramsAreWordSimilar(search, name)` became `@search <% name`, and that operator belongs to an
extension this database has never installed. **The C# is correct and the schema is not.** A unit
test with an in-memory fake would have passed here. Only a test against real Postgres finds out
that the database can't answer the question.

**Caveman version:** new trick need new tool. Code use tool. Cave not have tool. Cave say "what
tool?"

**Why `<%` and not `%`.** `pg_trgm` has two questions it can ask. `%` asks whether two whole
strings are similar. `<%` asks whether the search is similar to *some run of words* inside the
name. A customer types a word, not a name. Against *"Millennium Falcon Ultimate Collector Series"*,
the search *"falcon"* shares very little with the whole string, but it matches one of its words
exactly. `<%` is the question the customer is actually asking.

## Step 4 — Green: the extension, the index, and the Azure setting

**Not driven by a test — infrastructure.** The red test from step 3 is what tells us it is needed.

The extension belongs to the model, so EF puts it in a migration:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — the anchor, already there
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContext.cs — what it becomes
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trigram matching for the catalog search. Ships with Postgres; still has to be switched on.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
```

And an index for it, next to the column it indexes:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogSetConfiguration.cs — the anchor, already there
        builder.Property(set => set.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogSetConfiguration.cs — paste directly below it
        // A trigram index, not a B-tree: a B-tree can answer "starts with", but not "shares most of
        // its three-letter runs with". Kept although today's plan declines it: a search term is
        // selective, and this table gets a few inserts a week.
        builder.HasIndex(set => set.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_catalog_sets_name_trgm");

```

Generate the migration:

```bash
dotnet ef migrations add AddSetNameSearch \
  --project src/Catalog/BrickShare.Catalog.Api
```

Read what came out before running anything. The `Up` should have **two things, in this order**:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Migrations/…_AddSetNameSearch.cs — generated; what to check
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_sets_name_trgm",
                table: "catalog_sets",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
```

The extension comes first, because `gin_trgm_ops` is defined by it and the index cannot be created
without it. The `Down` reverses both. That means rolling this migration back **drops the extension
for the whole database**, and that is fine only while nothing else uses it. Nothing does today.

Run the tests. **Green, all three rows.** Testcontainers applies every migration before the first
test (episode 17), so the test database got the extension the same way production will.

**Caveman version:** tell cave to fetch tool. Write it in migration, so every cave fetch same tool
same way: test cave, laptop cave, Azure cave.

### Look at the plan, the way episode 34 did

Episode 34 declined three indexes after reading a query plan. This one gets the same treatment. In
`psql`:

```sql
explain analyze
select * from catalog_set_listings where 'titanc' <% name order by name, id limit 24;
```

Look for the filter on the **scan of `catalog_sets`**, below the aggregate. That is the same view
pushdown episode 34 showed with `piece_count`. It will be a `Seq Scan`: the table is a page or two,
and reading it costs less than reading an index first. Now make the planner show whether it
**could** use the index:

```sql
set enable_seqscan = off;
explain analyze
select * from catalog_set_listings where 'titanc' <% name order by name, id limit 24;
reset enable_seqscan;
```

`Bitmap Index Scan on ix_catalog_sets_name_trgm`. **That is the check that matters today.** The
index matches the query's operator and column, so it will be used once the table is big enough.
An index the query *cannot* use is the worst kind: it costs a write on every insert and nothing ever
reads it. That is what you would get from wrapping the column in `lower(name)`, or from choosing
the B-tree default.

### Why this index, when episode 34 declined three

This looks like it contradicts episode 34's rule, *look before you index*, because the plan above
shows the index being declined. It is a judgement call, so here is the reasoning:

| | Episode 34's filters | Search |
| --- | --- | --- |
| **Selectivity** | *"At least 1000 pieces"* matches most sets. Even at scale the planner would scan | A search term matches a handful of sets. This is the **selective filter** episode 34 named as its reason to come back |
| **Cost per row without an index** | One integer comparison | Cutting every name into trigrams, on every request |
| **Write cost** | Paid on every insert | Also paid on every insert, but `catalog_sets` gets **a few inserts a week** |

Episode 34 declined indexes that would **never** pay, at any size. This one pays as soon as the
catalog grows, and it costs almost nothing until then. What we can't do is prove it helps today. The
plan shows it being declined, and that is said out loud rather than hidden. **Close call; the
asymmetry decides it.**

**Caveman version:** old maps: even in big cave, nobody read them. This map: in big cave, everyone
read it. And cave get new rock only few times a week, so drawing map cost almost nothing.

### The setting Azure needs first

Postgres in Compose and in Testcontainers runs as a superuser, and a superuser can install any
extension that ships with the server. **Azure Database for PostgreSQL Flexible Server does not
allow that by default.** Extensions have to be named in the server parameter `azure.extensions`
first. Otherwise the migration job fails in the pipeline with an error that no local run can
reproduce:

```hcl
# infra/main.tf — the anchor, already there
resource "azurerm_postgresql_flexible_server_active_directory_administrator" "catalog" {
```

```hcl
# infra/main.tf — paste directly above it
# Flexible Server only installs extensions named here. pg_trgm is the catalog search (episode 36).
# A comma-separated list: the next extension is added to this value, not to a second resource.
resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.catalog.id
  value     = "PG_TRGM"
}

```

**Now the order it has to ship in.** Open `.github/workflows/deploy.yml` and read the job order:
`migrate` runs **before** `deploy`, and `deploy` is the job that runs `terraform apply`. Episode 19
built it that way on purpose: the schema has to exist before the code that uses it. So if this
Terraform resource and this migration go out in the same push, the migration runs against a server
that has not been told about `pg_trgm` yet, and fails.

**So it goes out in two pushes.** Commit `infra/main.tf` on its own and push it. That run's
`migrate` job has nothing new to apply, and its `deploy` job allows the extension. Then push the
rest. It is the same rule episode 19 applied to the schema, one level down: **the thing a change
depends on has to be live before the change is.**

The structural fix is a pipeline that applies infrastructure before migrating and deploys the image
after. It is a real improvement, and it belongs to episode 42, which rebuilds the pipeline anyway.
Once a year, for one setting, two pushes is the honest price.

**Caveman version:** Azure cave guard say "only tools on my list come in". Put tool on list first.
Next day, bring tool. Same day: guard say no.

## Step 5 — Red → green: the number on the box

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly below the "Titanc" row
    [InlineData("10294", new[] { "10294-1" })]
```

Red: `[]`. Nobody's name contains *"10294"*.

A set number is not like a name, though, and matching it the way names are matched would be a bug.
**A name is prose typed from memory. A set number is an identifier copied off a box.** Trigrams
would happily find *"10295"* as similar to *"10294-1"*: four of its six trigrams are shared. That
hands the customer the Titanic when they asked for a different set. An identifier is either right
or wrong, so it gets an exact match, on its start:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in WhereEveryFilterButTheme
            listings = listings.Where(listing => EF.Functions.TrigramsAreWordSimilar(search, listing.Name));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
            string numberPrefix = search.ToUpperInvariant();

            listings = listings.Where(listing =>
                EF.Functions.TrigramsAreWordSimilar(search, listing.Name)
                || listing.SetNumber.StartsWith(numberPrefix));
```

Green.

**Why `ToUpperInvariant` and not `ILike`.** The column never holds a lower-case set number, because
`SetNumber.TryParse` upper-cases every one before it is stored. The domain has already decided the
canonical form, so the search is converted into that form, and a plain comparison is enough. There
are no case rules at query time, because the case was settled at write time. **`StartsWith` also
escapes `%` and `_` for us.** EF translates it so that a customer typing `%` searches for a percent
sign and does not match everything.

Now the row that proves the point, and **it passes as soon as it is written**:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly below the "10294" row
    [InlineData("10295", new string[] { })]
```

A different set's number finds nothing. It passes because of the decision above, and it stays in the
suite so that nobody "simplifies" the two predicates into one trigram match later.

**Caveman version:** name is story, customer tell story wrong a bit, still find. Number is scratch
mark on rock. Wrong scratch, wrong rock. Never guess scratch.

### Refactor: a customer who typed only spaces

One more row:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly below the "10295" row
    [InlineData(" ", new[] { "10318-1", "41704-1", "10294-1" })]
```

Red: `[]`. A search box with a stray space in it is not a request for nothing. It is an empty
search, and an empty search is no search. The final shape of the predicate:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, the whole search block in WhereEveryFilterButTheme
        if (query.Search is { } search)
        {
            string numberPrefix = search.ToUpperInvariant();

            listings = listings.Where(listing =>
                EF.Functions.TrigramsAreWordSimilar(search, listing.Name)
                || listing.SetNumber.StartsWith(numberPrefix));
        }
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();

            // A name is prose typed from memory, so it is matched loosely, by trigram word similarity.
            // A set number is an identifier, stored upper-case by SetNumber, so it is matched exactly,
            // on its start. Otherwise 10295 would find the Titanic.
            string numberPrefix = search.ToUpperInvariant();

            listings = listings.Where(listing =>
                EF.Functions.TrigramsAreWordSimilar(search, listing.Name)
                || listing.SetNumber.StartsWith(numberPrefix));
        }
```

Green, all six rows.

**Caveman version:** customer press space, walk away. Not mean "show nothing". Mean "show all".

## Step 6 — The counts follow the search, and the service that is not here

Episode 35 said search would go into `WhereEveryFilterButTheme` without anyone having to remember
why. Here is the test for it, and **it passes as soon as it is written**:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Fact]
    public async Task A_search_narrows_the_theme_counts_too()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
            "/api/v1/catalog/sets?search=Titanic", Database.Api.Json);

        Assert.NotNull(page);
        Assert.Equal(["10294-1"], page.Sets.Select(set => set.SetNumber));
        Assert.Equal([("Friends", 0), ("Icons", 1)], page.Themes.Select(theme => (theme.Name, theme.SetCount)));
    }

```

A customer who searched *"Titanic"* sees one set under Icons and none under Friends. Step 1 put the
predicate in the only place a filter can go, and **the structure made the right answer the default
one.** That is what episode 35's refactor was for. It stays in the suite as the rule written down.

**Caveman version:** last time built one box for all sieves. Today new sieve go in box. Counts
right by themselves. Nobody remember anything.

**Why there is no search service.** `docs/architecture/catalog.md` puts it plainly: *"The reflex is
'search feature ⇒ search service', and resisting it is more useful to learn than another resource
in the diagram."* Azure AI Search earns its place with relevance tuning, synonyms, typo tolerance
at scale, and faceting over large corpora. This episode just did typo tolerance with one extension
and one index. Episode 35 did the faceting with one query. What a search service would add here is
**a second copy of the catalog**, kept in sync with the first by something that has to be built,
monitored and paid for. Nothing in a shop of a few thousand sets pays for that sync.

The point at which that answer changes can be measured: relevance the customer complains about,
synonyms (*"Star Wars"* and *"SW"*), languages, or a catalog in the hundreds of thousands. None of
those is here.

---

> **Seam — 36a ends here.** If recording in two parts: every test is green, search works by name,
> with a typo, and by number, and the counts follow it. 36b opens on episode 34's unfinished
> sentence: *"`limit` caps the list and nothing continues it."*

---

# 36b — Page two

## Step 7 — Why keyset, not page numbers

**Not driven by a test.** This step is an argument, and it decides the shape of everything after
it.

The obvious API for page two is `?page=2`, which becomes `OFFSET 24`. It has two problems:

**It gets slower the deeper you go.** `LIMIT 24 OFFSET 960` makes Postgres produce the first 984
rows in order and throw 960 of them away. Through this view, that means counting the copies of 960
sets that nobody will see. At a few thousand sets that is still fast, and it is not the reason that
decides it today.

**It moves under the reader.** This is the reason that decides it. A customer reads page one. Staff
catalogue a new set whose name sorts near the top. Now every set has moved down one place, and page
two starts with the last set of page one, which the customer sees twice. Take a set away instead and
one is skipped, and the customer never sees it at all. The shop's catalog changes during opening
hours, which is exactly when customers are scrolling it.

**Keyset paging asks a different question.** It does not ask for rows 25 to 48. It asks for the
next 24 rows **after the last one I saw**:

```sql
-- offset: "skip 24"
select * from catalog_set_listings order by name, id limit 24 offset 24;

-- keyset: "after Concorde"
select * from catalog_set_listings
where (name, id) > ('Concorde', '0199…')
order by name, id limit 24;
```

A set catalogued ahead of the reader's position is behind them, and it cannot push anything
forward. The `WHERE` is on columns the view pushes down, so the cost does not grow with depth.

This only works because **episode 34 made the order total**. `ThenBy(listing => listing.Id)` gives
every row exactly one position. Two sets called *"Titanic"* would otherwise sit in an order Postgres
is free to change between requests, and a cursor pointing at one of them would be ambiguous.

**What it costs, stated:** no *jump to page 17*, and no *page 3 of 12*. For a customer scrolling a
shop, *next* is the only button that matters. And the total is already on the page: episode 35's
theme counts add up to it, or say it outright when a theme is chosen.

**Caveman version:** "give me rock 25 to 48" break when someone add rock at front. "Give me rocks
after this rock" never break. Put finger on rock. Next time start from finger.

## Step 8 — Red → green: follow `next` to the end

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Fact]
    public async Task Following_next_visits_every_set_once_and_never_an_empty_page()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        List<string> seen = [];
        int pagesRead = 0;
        string? next = null;

        // Capped at five, so a cursor that never runs out fails the test instead of hanging it.
        do
        {
            string after = next is null ? "" : $"&after={next}";

            BrowseSetsResponse? page = await client.GetFromJsonAsync<BrowseSetsResponse>(
                $"/api/v1/catalog/sets?limit=1{after}", Database.Api.Json);

            Assert.NotNull(page);

            seen.AddRange(page.Sets.Select(set => set.SetNumber));
            pagesRead++;
            next = page.Next;
        }
        while (next is not null && pagesRead < 5);

        Assert.Equal(["10318-1", "41704-1", "10294-1"], seen);
        Assert.Equal(3, pagesRead);
    }

```

Red, a build error: `BrowseSetsResponse` has no `Next`. The test names the shape before it exists.

Two assertions, and the second is the subtle one. **Three sets at one a page is three pages, not
four.** The naive rule, *"the page is full, so there is probably more"*, sends the client to a
fourth, empty page every time the catalog divides evenly by the page size. That is a wasted request
at best, and at worst a UI that shows *"no results"* at the bottom of a list that had results.

**Caveman version:** walk cave one rock at time. See every rock once. Stop when rocks stop. No
walk into empty cave at end.

### The cursor

A new file. The cursor is **where the last page ended**, in the listing's order: a name and an id.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseCursor.cs — new file, all of it
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace BrickShare.Catalog.Api.Endpoints;

/// <summary>
/// Where a page of the catalog ended, in the order the catalog is listed: by name, then by id.
/// Clients receive it as an opaque string and hand it back unchanged. Opaque is not secret: anyone
/// can decode it, and it holds nothing that was not already on the page.
/// </summary>
public sealed record BrowseCursor(string Name, Guid Id)
{
    // Base64Url, not Base64: '+', '/' and '=' all mean something in a query string.
    public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

    public static bool TryDecode(string? value, [NotNullWhen(true)] out BrowseCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            cursor = JsonSerializer.Deserialize<BrowseCursor>(Base64Url.DecodeFromChars(value));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }

        // Valid JSON that is not a cursor, such as {}, decodes with no name.
        if (cursor is not { Name: not null })
        {
            cursor = null;
            return false;
        }

        return true;
    }
}
```

**Why opaque, when it is only a name and an id?** If clients built cursors themselves, the format
would be a public contract. Adding a third sort key, or ranking by relevance one day, would then
break every client. An opaque string that clients only pass back leaves the format ours to change.
**It is a boundary, not a secret.** Anyone can base64-decode it and read a set name that was on the
page they just looked at.

Then the request and the response. The parameter, with its documentation:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — the anchor, already there
/// <param name="Limit">How many sets to return, 1 to 50. 24 when absent.</param>
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — paste directly below it
/// <param name="After">The next value from the previous page, unchanged. Absent for the first page.</param>
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — the anchor, already there
    bool? AvailableNow,
    int? Limit)
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — what it becomes
    bool? AvailableNow,
    int? Limit,
    string? After)
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, at the bottom of the file
public sealed record BrowseSetsResponse(
    IReadOnlyList<SetListingResponse> Sets,
    IReadOnlyList<ThemeFacetResponse> Themes);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
public sealed record BrowseSetsResponse(
    IReadOnlyList<SetListingResponse> Sets,
    IReadOnlyList<ThemeFacetResponse> Themes,
    string? Next);
```

Episode 34 made the response an object rather than a bare array, saying *"episode 36 adds a `next`
cursor next to them"*. This is that field, added without breaking a single client.

### The handler

Now `BrowseSetsAsync`. **Where the cursor goes is episode 35's rule:** it narrows the page, and
never the counts, so it is applied *after* `everyFilterButTheme` is captured:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in BrowseSetsAsync
        IQueryable<CatalogSetListing> listings = query.ThemeId is { } themeId
            ? everyFilterButTheme.Where(listing => listing.ThemeId == themeId)
            : everyFilterButTheme;

        List<SetListingResponse> sets = await listings
            .OrderBy(listing => listing.Name)
            .ThenBy(listing => listing.Id)
            .Take(query.Limit ?? BrowseQuery.DefaultLimit)
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        IQueryable<CatalogSetListing> listings = query.ThemeId is { } themeId
            ? everyFilterButTheme.Where(listing => listing.ThemeId == themeId)
            : everyFilterButTheme;

        // The cursor narrows the page and never the counts, so it is applied here and not in
        // WhereEveryFilterButTheme. It looks like a filter. It is a position.
        if (BrowseCursor.TryDecode(query.After, out BrowseCursor? after))
        {
            // (name, id) > (@name, @id): the ORDER BY below, written as a comparison.
            listings = listings.Where(listing => EF.Functions.GreaterThan(
                ValueTuple.Create(listing.Name, listing.Id),
                ValueTuple.Create(after.Name, after.Id)));
        }

        int limit = query.Limit ?? BrowseQuery.DefaultLimit;

        // One more than the page holds. If it comes back, there is a next page, and a client
        // following next is never sent to an empty one.
        List<SetListingResponse> sets = await listings
            .OrderBy(listing => listing.Name)
            .ThenBy(listing => listing.Id)
            .Take(limit + 1)
```

Then, between the page and the counts:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, in BrowseSetsAsync
        List<ThemeFacetResponse> themes = await database.Themes
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly above it
        string? next = null;

        if (sets.Count > limit)
        {
            sets.RemoveAt(limit);
            next = new BrowseCursor(sets[^1].Name, sets[^1].Id).Encode();
        }

```

And the return:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, the last line of BrowseSetsAsync
        return TypedResults.Ok(new BrowseSetsResponse(sets, themes));
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
        return TypedResults.Ok(new BrowseSetsResponse(sets, themes, next));
```

Green.

**Caveman version:** fetch one rock more than page. Extra rock there? Then more cave. Put it back,
put finger on last real rock, give finger to customer.

Three lines are worth a sentence each:

- **`EF.Functions.GreaterThan` with two tuples.** The hand-written version is
  `name > @name OR (name = @name AND id > @id)`. It can't even be written in C#, because `Guid` has no
  `>` operator. Npgsql translates the tuple form into a Postgres **row-value comparison**,
  `(name, id) > (@name, @id)`. That is the `ORDER BY name, id` written as a comparison, which makes it
  hard to get wrong.
- **Every comparison happens in Postgres.** The cursor carries the name as data, and nothing in C#
  ever compares two names. That matters because .NET's culture-aware ordering and Postgres's
  `en_US.utf8` collation disagree about punctuation and case. If the order came from Postgres and
  the comparison from .NET, sets would be skipped. It is episode 17's collation lesson again,
  from the other side.
- **`Take(limit + 1)`** is how `next` knows the difference between *"the page is full"* and
  *"there is more"*, which is what the test's `pagesRead == 3` pins.

## Step 9 — Red → green: a cursor we did not issue

A mangled cursor, a truncated copy-paste or a hand-written guess currently fails to decode, so
`TryDecode` returns false and the handler silently serves **page one**. A client in a loop would
then walk the catalog from the start forever. It should be a `400`, and the theory for refused
requests already exists:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — the anchor, in A_filter_that_cannot_match_anything_is_refused
    [InlineData("?maxPrice=-1", "maxPrice")]
```

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly below it
    [InlineData("?after=not-a-cursor", "after")]
```

Red: `Expected: BadRequest, Actual: OK`.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — the anchor, the last rule in BrowseQueryValidator
        RuleFor(query => query.Limit).InclusiveBetween(1, BrowseQuery.MaxLimit)
            .WithMessage($"A page holds 1 to {BrowseQuery.MaxLimit} sets.");
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseQuery.cs — paste directly below it

        RuleFor(query => query.After)
            .Must(after => BrowseCursor.TryDecode(after, out _))
            .When(query => query.After is not null)
            .WithMessage("after is not a cursor this API issued. Pass next from the previous page unchanged.");
```

Green. The validator decodes once to refuse, and the handler decodes again to use it. That is the
cost of keeping validation in the one place episode 30 put it, and decoding a few dozen bytes twice
is a cost worth paying for that.

**Caveman version:** customer bring fake finger. Before: server pretend it real, start from front
of cave. Now: server say "not my finger".

## Step 10 — The two rules paging must not break

Two more tests, and **both pass as soon as they are written.** They describe rules and don't drive
the design.

**The page does not shift.** Step 7's argument, as a test:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Fact]
    public async Task A_set_catalogued_mid_scroll_does_not_shift_the_next_page()
    {
        HttpClient client = Database.Api.CreateClient();
        await Database.CatalogueAsync(client, StockedSet.MainStreetBuilding);
        await Database.CatalogueAsync(client, StockedSet.Titanic);

        BrowseSetsResponse? first = await client.GetFromJsonAsync<BrowseSetsResponse>(
            "/api/v1/catalog/sets?limit=1", Database.Api.Json);

        Assert.NotNull(first);
        Assert.Equal(["41704-1"], first.Sets.Select(set => set.SetNumber));

        // Concorde sorts ahead of the page the customer has already read. With ?page=2 everything
        // would move down one place, and Main Street Building would be served a second time.
        await Database.CatalogueAsync(client, StockedSet.Concorde);

        BrowseSetsResponse? second = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?limit=1&after={first.Next}", Database.Api.Json);

        Assert.NotNull(second);
        Assert.Equal(["10294-1"], second.Sets.Select(set => set.SetNumber));
        Assert.Null(second.Next);
    }

```

**The counts do not scroll.** Episode 35 left a test aimed at this moment, but it only covered
`limit`. The cursor needs its own:

```csharp
// tests/BrickShare.Catalog.IntegrationTests/Browse/BrowseSetsTests.cs — paste directly above StockTheShelfAsync
    [Fact]
    public async Task Page_two_counts_the_same_catalog_as_page_one()
    {
        HttpClient client = Database.Api.CreateClient();
        await StockTheShelfAsync(client);

        BrowseSetsResponse? first = await client.GetFromJsonAsync<BrowseSetsResponse>(
            "/api/v1/catalog/sets?limit=1", Database.Api.Json);

        Assert.NotNull(first);

        BrowseSetsResponse? second = await client.GetFromJsonAsync<BrowseSetsResponse>(
            $"/api/v1/catalog/sets?limit=1&after={first.Next}", Database.Api.Json);

        Assert.NotNull(second);
        Assert.Equal(first.Themes, second.Themes);
    }

```

**Now prove it bites, on camera.** The cursor looks like a filter, and it is exactly the kind of
line someone tidies into the filter method. Do that, temporarily. Cut the cursor block out of
`BrowseSetsAsync` and paste it into `WhereEveryFilterButTheme`:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — TEMPORARY, at the end of WhereEveryFilterButTheme, above "return listings;"
        if (BrowseCursor.TryDecode(query.After, out BrowseCursor? after))
        {
            listings = listings.Where(listing => EF.Functions.GreaterThan(
                ValueTuple.Create(listing.Name, listing.Id),
                ValueTuple.Create(after.Name, after.Id)));
        }
```

Red:

```text
Assert.Equal() Failure: Collections differ
Expected: [… { Name = "Friends", SetCount = 1 }, … { Name = "Icons", SetCount = 2 }]
Actual:   [… { Name = "Friends", SetCount = 1 }, … { Name = "Icons", SetCount = 1 }]
```

Page two thinks Icons holds one set, because Concorde is behind the cursor and was not counted. A
customer scrolling down would watch every count shrink toward zero. **A count that changes as you
scroll is counting the page, not the catalog.** Undo the move. Green again.

**Caveman version:** tidy person put finger-rule in sieve box. Now counts shrink as customer walk.
Test scream. Put finger-rule back where it was.

## Step 11 — Relevance, or a page that holds still

**Not driven by a test.** This is the trade-off the course plan asked to state rather than hide.

Search now **filters** by similarity but **orders** by name. A customer who types *"titanc"* gets
every set similar enough, in alphabetical order, not the best match first. The alternative is
obvious: `ORDER BY word_similarity(@search, name) DESC`, which puts the Titanic above a set that
only scraped past the threshold.

It would break the cursor. A keyset needs an order made of **stored values**. Similarity is
computed per request, from the search term, so a cursor would have to carry the score, re-rank
every page and hope nothing tied. It can be done, and it is the kind of thing a search service
exists to do well.

**For this shop, the stable page wins.** A search in a catalog of a few thousand sets returns a
handful of results, usually a single page, and when every result is on one screen the order
matters much less. Relevance does matter, and pretending otherwise would be dishonest. It matters
most when a search returns hundreds of results, and that is one of the signals from step 6 that
would bring the search-service question back. **Close call, decided by the shape of the data, and
revisited if the data changes.**

The same reasoning leaves `pg_trgm.word_similarity_threshold` at its default of 0.6. Tuning it
without real searches to tune against is guessing.

**Caveman version:** best-first order change every time, finger fall off. Name order never change,
finger stay. Few rocks per search, so order not matter much. Keep finger.

## Step 12 — Describe it, and run it

**Not driven by a test.** OpenAPI metadata and a request collection. The two new parameters
already have their `<param>` documentation from steps 1 and 8. The operation's description needs
to say what `search` matches and how `next` is used:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — the anchor, already there
            .WithDescription(
                "Sets by name, filtered by theme, piece count, age, price and availability. "
                + "startingPrice is the cheapest copy available now, and null when none is. "
                + "maxPrice therefore returns only sets with a copy available. "
                + "themes lists every theme with a set, each with the number of sets choosing it "
                + "would return under the other filters, including 0.")
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
            .WithDescription(
                "Sets by name, found by search and filtered by theme, piece count, age, price and "
                + "availability. search matches names loosely, tolerating a typo, and set numbers "
                + "by their start. "
                + "startingPrice is the cheapest copy available now, and null when none is. "
                + "maxPrice therefore returns only sets with a copy available. "
                + "themes lists every theme with a set, each with the number of sets choosing it "
                + "would return under the other filters and the search, including 0. "
                + "next is null on the last page. Pass it back as after, with the same filters, "
                + "for the page that follows.")
```

`next` is a new field on the response, and `search` and `after` are new optional parameters. **All
three are additive.** A client written against episode 35 keeps working without knowing any of
them exist.

Then the requests, appended at the end of `BrickShare.Catalog.Api.http`:

```http
### BrickShare.Catalog.Api.http — append at the end of the file

### Search by name. A typo still finds the Titanic.
GET {{host}}/api/v1/catalog/sets?search=titanc

### Search by the start of a set number.
GET {{host}}/api/v1/catalog/sets?search=10294

### Page one, two sets at a time. Copy "next" from the response into @after below.
GET {{host}}/api/v1/catalog/sets?limit=2

@after = paste-next-here

### Page two. The themes block is the same as page one's.
GET {{host}}/api/v1/catalog/sets?limit=2&after={{after}}
```

Run them. The shot to hold on is the **last** one: new sets on the page, and the `themes` block
unchanged from page one.

**Caveman version:** map say what new words mean. Old visitors not hurt. New visitors know how to
use finger.

---

## What this episode is not

**No relevance ranking.** Step 11 says why: it would break the cursor, and a shop's searches are
short.

**No threshold tuning, no synonyms, no stemming.** There are no real searches to tune against yet.
Those are also the first signals that would make Azure AI Search worth its second data store.

**No page numbers, no total page count.** Step 7: keyset gives them up, and the theme counts
already carry the total.

**No length limit on `search`.** A very long search costs more trigrams, but a public catalog
returns nothing sensitive, and there is no evidence of abuse to design against. If it ever shows up
in the metrics from episode 41, it is one validator rule.

**No `previous` cursor.** A customer going back uses the page their client already has.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green: a six-row search theory, four new facts, one new row in the refused theory, and every episode 34 and 35 test untouched |
| Step 2's row against step 1's code | `titanic` expected `10294-1`, actual `[]` |
| Step 3's code before the migration | `500`, `42883: operator does not exist: text <% text` |
| `dotnet ef migrations has-pending-model-changes` | No changes after `AddSetNameSearch` |
| `\dx` in `psql` | `pg_trgm` installed |
| `explain` with `enable_seqscan = off` | `Bitmap Index Scan on ix_catalog_sets_name_trgm` |
| `GET /catalog/sets?search=titanc` | The Titanic |
| `GET /catalog/sets?search=10295` | No sets |
| `GET /catalog/sets?search=Titanic` | Friends 0, Icons 1 |
| `GET /catalog/sets?limit=1`, following `next` | Three pages, the last with `next: null` |
| `GET /catalog/sets?after=not-a-cursor` | `400`, with an `after` error |
| Step 10's cursor moved into `WhereEveryFilterButTheme` | `Page_two_counts_the_same_catalog_as_page_one` red |
| First push (Terraform alone) | `azure.extensions = PG_TRGM` on the server, migrate job has nothing to apply |
| Second push | `AddSetNameSearch` applies in the migrate job without a permission error |
| `/openapi/v1.json` | `search` and `after` parameters, `next` on the response, the new description |

Two rows are worth running on camera, one from each half. **Step 2's red** is the collation bug
episode 17 predicted, finally caught. **Step 10's red** is the reason the cursor has to stay out of
the filter method.

## Next

[Episode 37 — Set detail, and the rules that are easy to break](catalog-api.md#episode-37--set-detail-and-the-rules-that-are-easy-to-break):
the set detail endpoint, reading its available count and starting price from episode 34's view
so the detail page and this listing cannot disagree about a price. And three rules from UC-7 that a
query written from intuition gets wrong.
