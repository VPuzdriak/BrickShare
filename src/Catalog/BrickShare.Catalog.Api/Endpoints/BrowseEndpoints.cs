using BrickShare.Catalog.Api.Persistence;

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
                "Sets by name, filtered by theme, piece count, age, price and availability. "
                + "startingPrice is the cheapest copy available now, and null when none is. "
                + "maxPrice therefore returns only sets with a copy available. "
                + "themes lists every theme with a set, each with the number of sets choosing it "
                + "would return under the other filters, including 0.")
            .ProducesValidationProblem();

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
}

public sealed record ThemesResponse(IReadOnlyList<ThemeResponse> Themes);

public sealed record ThemeResponse(Guid Id, string Name);

public sealed record BrowseSetsResponse(
    IReadOnlyList<SetListingResponse> Sets,
    IReadOnlyList<ThemeFacetResponse> Themes);

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
