// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — new file

using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http.HttpResults;

namespace BrickShare.Catalog.Api.Endpoints;

public static class CatalogLookupEndpoints
{
    public static RouteGroupBuilder MapCatalogLookups(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog/lookups")
            .WithTags("Catalog lookups");

        group.MapPost("/", LookUpAsync)
            .AddEndpointFilter<ValidationFilter<LookupRequest>>();

        return group;
    }

    private static async Task<Results<Created<LookupResponse>, ProblemHttpResult>> LookUpAsync(
        LookupRequest request,
        IRebrickableCatalog rebrickable,
        CatalogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        SetNumber number = SetNumber.Parse(request.SetNumber);

        RebrickableSet? set = await rebrickable.FindSetAsync(number, cancellationToken);
        if (set is null)
        {
            return TypedResults.Problem(
                title: "Set not found",
                detail: $"Rebrickable has no set numbered {number}. Set numbers on the box usually end in -1.",
                statusCode: StatusCodes.Status404NotFound);
        }

        RebrickableTheme? theme = await rebrickable.FindThemeAsync(set.ThemeId, cancellationToken);
        if (theme is null)
        {
            return TypedResults.Problem(
                title: "Set facts incomplete",
                detail:
                $"Rebrickable knows set {number} but not its theme ({set.ThemeId}), so the draft would be incomplete.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        RebrickableSnapshot snapshot = RebrickableSnapshot.Capture(set, theme, clock.GetUtcNow());

        database.Snapshots.Add(snapshot);
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/catalog/lookups/{snapshot.Id}", LookupResponse.From(snapshot));
    }
}

public sealed record LookupRequest(string SetNumber);

public sealed record LookupResponse(
    Guid LookupId,
    string SetNumber,
    string Name,
    string Theme,
    int Year,
    int PieceCount,
    Uri? ImageUrl,
    DateTimeOffset FetchedAt)
{
    public static LookupResponse From(RebrickableSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.Number.Value,
        snapshot.Name,
        snapshot.ThemeName,
        snapshot.Year,
        snapshot.PieceCount,
        snapshot.ImageUrl,
        snapshot.FetchedAt);
}
