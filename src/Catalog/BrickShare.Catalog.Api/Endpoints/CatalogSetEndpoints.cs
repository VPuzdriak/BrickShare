using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using FluentValidation;
using FluentValidation.Results;

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

        group.MapPost("/", CatalogueAsync);

        return group;
    }

    private static async Task<Results<Created<CatalogSetResponse>, ValidationProblem, ProblemHttpResult>>
        CatalogueAsync(
            CatalogueSetRequest request,
            IValidator<CatalogueSetRequest> validator,
            CatalogDbContext database,
            CancellationToken cancellationToken)
    {
        ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

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
/// What staff send to catalogue a set. Every product fact in here is client-supplied, which is a security problem
/// </summary>
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
