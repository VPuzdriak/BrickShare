using BrickShare.Catalog.Api.Persistence;
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

    private static async Task<Results<Created<CatalogSetResponse>, ValidationProblem>> CatalogueAsync(
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

        database.Sets.Add(catalogSet);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsAlreadyCatalogued(ex))
        {
            throw new DomainRuleViolationException($"Set {request.SetNumber} is already catalogued.", ex);
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
/// What staff send to catalogue a set. Every product fact in here is client-supplied, which is
/// a security problem episode 26 exists to fix.
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
