using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace BrickShare.Catalog.Api.Endpoints;

public static class CatalogSetEndpoints
{
    public static RouteGroupBuilder MapCatalogSets(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog/sets")
            .WithTags("Catalog sets");

        group.MapPost("/", CatalogueAsync)
            .AddEndpointFilter<ValidationFilter<CatalogueSetRequest>>()
            .WithSummary("Catalogue a set the shop will rent out")
            .WithDescription(
                "Turns a lookup into a catalogued set. Prices are not negative, minimumRentalDays "
                + "is at least 1, and minimumAge is between 0 and 18 — none of which the schema "
                + "below can say, because those rules live in CatalogueSetRequestValidator.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return group;
    }

    private static async Task<Results<Created<CatalogSetResponse>, ProblemHttpResult>>
        CatalogueAsync(
            CatalogueSetRequest request,
            CatalogDbContext database,
            CancellationToken cancellationToken)
    {
        RebrickableSnapshot? snapshot =
            await database.Snapshots.FindAsync([request.LookupId], cancellationToken);

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
            snapshot.Name,
            snapshot.ThemeName,
            snapshot.Year,
            snapshot.PieceCount,
            new Money(request.RetailPrice),
            new Money(request.BaseRentalPrice),
            request.MinimumRentalDays,
            request.MinimumAge);

        database.Sets.Add(catalogSet);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsAlreadyCatalogued(ex))
        {
            throw new DomainRuleViolationException($"Set {snapshot.Number} is already catalogued.", ex);
        }

        return TypedResults.Created($"/api/v1/sets/{catalogSet.Id}", CatalogSetResponse.From(catalogSet));
    }

    private static bool IsAlreadyCatalogued(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_catalog_sets_set_number"
        };
}

/// <summary>
/// What staff send to catalogue a set: the id of an earlier lookup, and the commercial terms.
/// </summary>
/// Every product fact behind that lookup is client-supplied, which is a security problem
public sealed record CatalogueSetRequest(
    Guid LookupId,
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
