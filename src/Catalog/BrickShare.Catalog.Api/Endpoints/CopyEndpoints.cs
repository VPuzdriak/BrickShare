using System.Diagnostics;

using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

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

        group.MapPost("/", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterCopiesRequest>>();

        return group;
    }

    private static async Task<Results<Created<RegisterCopiesResponse>, ProblemHttpResult>>
        RegisterAsync(
            Guid setId,
            RegisterCopiesRequest request,
            ILabelCodeMinter minter,
            CatalogDbContext database,
            CancellationToken cancellationToken)
    {
        bool setExists = await database.Sets.AnyAsync(set => set.Id == setId, cancellationToken);
        if (!setExists)
        {
            return TypedResults.Problem(
                title: "No such set",
                detail: $"Set {setId} is not catalogued. Catalogue it first at /api/v1/catalog/sets.",
                statusCode: StatusCodes.Status404NotFound);
        }

        IReadOnlyList<Copy> copies =
            await RegisterWithMintedLabelsAsync(setId, request, minter, database, cancellationToken);

        return TypedResults.Created(
            $"/api/v1/catalog/sets/{setId}/copies",
            new RegisterCopiesResponse([.. copies.Select(CopyResponse.From)]));
    }

    private static async Task<IReadOnlyList<Copy>> RegisterWithMintedLabelsAsync(Guid setId,
        RegisterCopiesRequest request,
        ILabelCodeMinter minter,
        CatalogDbContext database,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MintAttempts; attempt++)
        {
            List<Copy> copies =
            [
                .. request.Copies.Select(copy =>
                    Copy.Register(setId, minter.Mint(), copy.Grade, copy.BaselineWeightInGrams))
            ];

            database.Copies.AddRange(copies);

            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return copies;
            }
            catch (DbUpdateException ex) when (IsLabelAlreadyTaken(ex) && attempt < MintAttempts)
            {
                // Step 4 has something to say about this line.
                foreach (Copy copy in copies)
                {
                    database.Entry(copy).State = EntityState.Detached;
                }
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

public sealed record RegisterCopiesRequest(IReadOnlyList<CopyToRegister> Copies);

public sealed record CopyToRegister(ConditionGrade Grade, int BaselineWeightInGrams);

public sealed record RegisterCopiesResponse(IReadOnlyList<CopyResponse> Copies);

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
