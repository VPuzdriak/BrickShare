# Catalog browsing and search

Customers browse the catalog without signing in. They search set names with typo tolerance, filter, sort, page, and see how many instances are ready to book now.

| Item | Value |
|------|-------|
| Actors | Customer (anonymous) |
| Services | Catalog, Reservation (availability) |
| Architecture | [Catalog Service](../Architecture.md#32-catalog-service) |

## Rules

- Only active sets (`IsActive`) are listed.
- **Fuzzy search on the set name** and the LEGO set number from the box. "milenium falcn" (two typos) finds "Millennium Falcon". A search term matches any part of the name, so "falcon" finds "Millennium Falcon".
- Filters: theme, age (`AgeFrom <= age`), piece range, daily price range.
- With search text, results sort by match score. With no search text, results sort by the chosen field (name, pieces, price, newest).
- Paging is classic `page` / `pageSize` with a total count.
- Facet counts for the filter sidebar (sets per theme, age bands) are cached for a few minutes.
- Availability is the number of instances ready to book now (see [Reservation and payment](06-reservation-and-payment.md#instance-availability)).

## Endpoints

| Method | Route | Policy | Purpose |
|--------|-------|--------|---------|
| GET | `/catalog/sets?q&themeId&age&minPieces&maxPieces&minPrice&maxPrice&sort&page&pageSize` | Anonymous | Fuzzy search, filter, sort and page sets |
| GET | `/catalog/themes` | Anonymous | Themes with set counts (filter sidebar) |
| GET | `/catalog/sets/{id}` | Anonymous | Set details |
| GET | `/catalog/sets/{id}/availability` | Anonymous | Proxy to Reservation: number of instances ready to book now |
| GET | `/reservations/availability?setId` | Anonymous | Number of instances ready to book now |

## Implementation notes

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

-- typo-tolerant search on name and LEGO set number
CREATE INDEX ix_sets_name_trgm ON lego_sets USING gin (lower(name) gin_trgm_ops);
CREATE INDEX ix_sets_number_trgm ON lego_sets USING gin (lego_set_number gin_trgm_ops);

-- filters and sorting
CREATE INDEX ix_sets_theme ON lego_sets (theme_id) WHERE is_active;
CREATE INDEX ix_sets_pieces ON lego_sets (pieces) WHERE is_active;
CREATE INDEX ix_sets_age ON lego_sets (age_from) WHERE is_active;
CREATE INDEX ix_sets_attributes ON lego_sets USING gin (attributes jsonb_path_ops);
```

Example query: "milenium falcn", theme Star Wars, age 10, 1000 to 8000 pieces.

```sql
SELECT s.id, s.lego_set_number, s.name, s.pieces, s.age_from, s.daily_price,
       word_similarity(lower(@q), lower(s.name)) AS score
FROM lego_sets s
WHERE s.is_active
  AND (lower(@q) <% lower(s.name) OR s.lego_set_number % @q)
  AND s.theme_id = @themeId
  AND s.age_from <= @age
  AND s.pieces BETWEEN @minPieces AND @maxPieces
ORDER BY score DESC, s.name
LIMIT 20 OFFSET 0;
```

- `<%` (word similarity) uses the threshold `pg_trgm.word_similarity_threshold` (default 0.6). Tune it to about 0.4 for short names.
- In EF Core, the `Npgsql.EntityFrameworkCore.PostgreSQL.Trigrams` plugin exposes `EF.Functions.TrigramsWordSimilarity(...)`, so the query stays in LINQ. Filters are added only when the client sends them.
- The catalog is small (hundreds to a few thousand sets), so `OFFSET` paging is fine.
- Facet counts come from `GROUP BY` queries, cached in memory.
