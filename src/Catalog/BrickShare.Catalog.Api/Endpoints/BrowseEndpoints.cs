using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;
using BrickShare.Catalog.Domain.Pricing;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Endpoints;

/// <summary>
/// What a customer can read. Kept apart from the staff groups on purpose
/// </summary>
public static class BrowseEndpoints
{
    public static RouteGroupBuilder MapBrowse(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog")
            .WithTags("Browse");

        group.MapGet("/themes", ListThemesAsync)
            .WithSummary("List the themes a customer can filter by")
            .WithDescription("Every theme with at least one catalogued set, by name.");

        group.MapGet("/sets", BrowseSetsAsync)
            .AddEndpointFilter<ValidationFilter<BrowseQuery>>()
            .WithSummary("Browse the catalog")
            .WithDescription(
                "Sets by name, found by search and filtered by theme, piece count, age, price and "
                + "availability. search matches names loosely, tolerating a typo, and set numbers "
                + "by their start. "
                + "startingPrice is the cheapest copy available now, and null when none is. "
                + "maxPrice therefore returns only sets with a copy available. "
                + "themes lists every theme with a set, each with the number of sets choosing it "
                + "would return under the other filters and the search, including 0. "
                + "next is null on the last page. Pass it back as after, with the same filters, "
                + "for the page that follows.");

        // src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — paste directly above it
        group.MapGet("/sets/{setId:guid}", GetSetAsync)
            .WithSummary("View a set and every copy of it")
            .WithDescription(
                "Product facts, with availableCount and startingPrice as the listing shows them. "
                + "copies lists every copy the shop rents out, available or not, cheapest first, "
                + "each with its own rentalPrice and deposit. available says whether a copy can be "
                + "reserved now; every copy listed can be subscribed to. A set with no copies has "
                + "an empty list and a null startingPrice. Retired copies are not listed.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<ThemesResponse>> ListThemesAsync(
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        List<ThemeResponse> themes = await database.Themes
            .Where(theme => database.Sets.Any(set => set.ThemeId == theme.Id))
            .OrderBy(theme => theme.Name)
            .Select(theme => new ThemeResponse(theme.Id, theme.Name))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ThemesResponse(themes));
    }

    private static async Task<Ok<BrowseSetsResponse>> BrowseSetsAsync(
        [AsParameters] BrowseQuery query,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        IQueryable<CatalogSetListing> everyFilterButTheme = WhereEveryFilterButTheme(database.Listings, query);

        IQueryable<CatalogSetListing> listings = query.ThemeId is { } themeId
            ? everyFilterButTheme.Where(listing => listing.ThemeId == themeId)
            : everyFilterButTheme;

        if (BrowseCursor.TryDecode(query.After, out BrowseCursor? after))
        {
            // (name, id) > (@name, @id): the ORDER BY below, written as a comparison.
            listings = listings.Where(listing => EF.Functions.GreaterThan(
                ValueTuple.Create(listing.Name, listing.Id),
                ValueTuple.Create(after.Name, after.Id)));
        }

        int limit = query.Limit ?? BrowseQuery.DefaultLimit;

        List<SetListingResponse> sets = await listings
            .OrderBy(listing => listing.Name)
            .ThenBy(listing => listing.Id)
            .Take(limit + 1)
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

        string? next = null;

        // When the next page exists, remove the extra set and return a cursor for the last set on this page.
        // We don't include the extra set in the response because it would be confusing to have a set on page 1 that is also on page 2.
        if (sets.Count > limit)
        {
            sets.RemoveAt(limit);
            next = new BrowseCursor(sets[^1].Name, sets[^1].Id).Encode();
        }

        List<ThemeFacetResponse> themes = await database.Themes
            .Where(theme => database.Sets.Any(set => set.ThemeId == theme.Id))
            .OrderBy(theme => theme.Name)
            .Select(theme => new ThemeFacetResponse(
                theme.Id,
                theme.Name,
                everyFilterButTheme.Count(listing => listing.ThemeId == theme.Id)))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new BrowseSetsResponse(sets, themes, next));
    }

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

        var prices = await database.Sets
            .Where(set => set.Id == setId)
            .Select(set => new { set.BaseRentalPrice, set.RetailPrice })
            .SingleAsync(cancellationToken);


        GradeMultipliers multipliers = await LoadGradeMultipliersAsync(database, cancellationToken);

        var copies = await database.Copies
            .AsNoTracking()
            .Where(copy => copy.CatalogSetId == setId && copy.Status != CopyStatus.Retired)
            .OrderBy(copy => copy.Id)
            .ToListAsync(cancellationToken);

        var onShow = copies
            .Select(copy => new SetCopyResponse(
                copy.Id,
                copy.Grade,
                copy.Status == CopyStatus.Available,
                PriceCalculator.RentalPrice(prices.BaseRentalPrice, copy.Grade, multipliers).Amount,
                PriceCalculator.Deposit(prices.RetailPrice, copy.Grade, multipliers).Amount))
            .OrderBy(copy => copy.RentalPrice)
            .ThenBy(copy => copy.Id)
            .ToList();

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
            onShow));
    }

    private static async Task<GradeMultipliers> LoadGradeMultipliersAsync(
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        Dictionary<ConditionGrade, decimal> rows = await database.GradeMultipliers
            .ToDictionaryAsync(row => row.Grade, row => row.Multiplier, cancellationToken);

        return new GradeMultipliers(rows);
    }



    private static IQueryable<CatalogSetListing> WhereEveryFilterButTheme(
        IQueryable<CatalogSetListing> listings,
        BrowseQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            string numberPrefix = search.ToUpperInvariant();

            listings = listings.Where(listing =>
                EF.Functions.TrigramsAreWordSimilar(search, listing.Name)
                || listing.SetNumber.StartsWith(numberPrefix));
        }

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
}

public sealed record ThemesResponse(IReadOnlyList<ThemeResponse> Themes);

public sealed record ThemeResponse(Guid Id, string Name);

public sealed record BrowseSetsResponse(
    IReadOnlyList<SetListingResponse> Sets,
    IReadOnlyList<ThemeFacetResponse> Themes,
    string? Next);

public sealed record SetListingResponse(
    Guid Id,
    string SetNumber,
    string Name,
    string Theme,
    int Year,
    int PieceCount,
    int MinimumAge,
    int MinimumRentalDays,
    int AvailableCount,
    decimal? StartingPrice);

public sealed record ThemeFacetResponse(Guid Id, string Name, int SetCount);

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

// src/Catalog/BrickShare.Catalog.Api/Endpoints/BrowseEndpoints.cs — what it becomes
public sealed record SetCopyResponse(
    Guid Id,
    ConditionGrade Grade,
    bool Available,
    decimal RentalPrice,
    decimal Deposit);
