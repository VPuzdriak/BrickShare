using System.Diagnostics;

using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace BrickShare.Catalog.Api.Endpoints;

public static class CopyEndpoints
{
    private const int MintAttempts = 3;

    public static RouteGroupBuilder MapCopies(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalog/sets/{setId:guid}/copies")
            .WithTags("Copies");

        group.MapPost("/", RegisterAsync);

        return group;
    }

    private static async Task<Results<Created<CopyResponse>, ValidationProblem, ProblemHttpResult>>
        RegisterAsync(
            Guid setId,
            RegisterCopyRequest request,
            IValidator<RegisterCopyRequest> validator,
            CatalogDbContext database,
            CancellationToken cancellationToken)
    {
        ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        bool setExists = await database.Sets.AnyAsync(set => set.Id == setId, cancellationToken);
        if (!setExists)
        {
            return TypedResults.Problem(
                title: "No such set",
                detail: $"Set {setId} is not catalogued. Catalogue it first at /api/v1/catalog/sets.",
                statusCode: StatusCodes.Status404NotFound);
        }

        Copy copy = await RegisterWithAMintedLabelAsync(setId, request, database, cancellationToken);

        return TypedResults.Created($"/api/v1/catalog/copies/{copy.Id}", CopyResponse.From(copy));
    }

    private static async Task<Copy> RegisterWithAMintedLabelAsync(
        Guid setId,
        RegisterCopyRequest request,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MintAttempts; attempt++)
        {
            Copy copy = Copy.Register(
                setId, LabelCode.Mint(), request.Grade, request.BaselineWeightInGrams);

            database.Copies.Add(copy);

            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return copy;
            }
            catch (DbUpdateException ex) when (IsLabelAlreadyTaken(ex) && attempt < MintAttempts)
            {
                // A failed SaveChanges leaves the entity Added. Without this the retry writes two
                // rows, one of which still carries the label that just collided.
                database.Entry(copy).State = EntityState.Detached;
            }
        }

        // The last attempt either returns or lets its exception out, because the filter above stops
        // catching once attempt reaches MintAttempts. The compiler cannot see that: definite-return
        // analysis does not reason about exception filters, so it needs to be told.
        throw new UnreachableException();
    }

    private static bool IsLabelAlreadyTaken(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_copies_label_code"
        };
}

public sealed record RegisterCopyRequest(ConditionGrade Grade, int BaselineWeightInGrams);

public sealed record CopyResponse(
    Guid Id,
    Guid CatalogSetId,
    string LabelCode,
    ConditionGrade Grade,
    CopyStatus Status,
    int BaselineWeightInGrams)
{
    public static CopyResponse From(Copy copy) => new(
        copy.Id,
        copy.CatalogSetId,
        copy.Label.Value,
        copy.Grade,
        copy.Status,
        copy.BaselineWeightInGrams);
}
