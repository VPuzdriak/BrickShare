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

        group.MapGet("/themes", ListThemesAsync);
        group.MapGet("/sets", BrowseSetsAsync);

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
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        List<SetListingResponse> sets = await database.Listings
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

        return TypedResults.Ok(new BrowseSetsResponse(sets));
    }
}

public sealed record ThemesResponse(IReadOnlyList<ThemeResponse> Themes);

public sealed record ThemeResponse(Guid Id, string Name);

public sealed record BrowseSetsResponse(IReadOnlyList<SetListingResponse> Sets);

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
