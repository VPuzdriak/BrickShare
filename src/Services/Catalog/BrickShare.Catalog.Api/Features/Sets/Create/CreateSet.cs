using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Api.Infrastructure.Validation;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.Sets.Create;

public static class CreateSet
{
    public static void MapCreateSet(this RouteGroupBuilder sets) =>
        sets.MapPost("/", Handle).AddEndpointFilter<ValidationFilter<SetRequest>>();

    private static async Task<Results<Created<SetResponse>, ProblemHttpResult>> Handle(
        SetRequest request,
        CatalogDbContext dbContext,
        ISetSpecificationProvider specifications,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (request.RebrickableSetNumber is not null)
        {
            return await SubmitDraft(
                request, request.RebrickableSetNumber, dbContext, specifications, timeProvider, cancellationToken);
        }

        var legoSet = request.ToLegoSet(Guid.CreateVersion7());

        dbContext.LegoSets.Add(legoSet);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/catalog/sets/{legoSet.Id}", SetResponse.From(legoSet));
    }

    private static async Task<Results<Created<SetResponse>, ProblemHttpResult>> SubmitDraft(
        SetRequest request,
        string rebrickableSetNumber,
        CatalogDbContext dbContext,
        ISetSpecificationProvider specifications,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var parsed = RebrickableSetNumber.Parse(rebrickableSetNumber);

        if (parsed.IsFailure)
        {
            return SetProblems.InvalidSetNumber(rebrickableSetNumber);
        }

        var existing = await dbContext.LegoSets.FirstOrDefaultAsync(
            legoSet => legoSet.RebrickableSetNum == parsed.Value.Value, cancellationToken);

        if (existing is not null)
        {
            return SetProblems.RebrickableSetAlreadyExists(existing.Id);
        }

        SetSpecification? specification;

        try
        {
            specification = await specifications.FindAsync(parsed.Value, cancellationToken);
        }
        catch (SetSpecificationUnavailableException)
        {
            return SetProblems.RebrickableUnavailable();
        }

        if (specification is null)
        {
            return SetProblems.UnknownSetNumber(rebrickableSetNumber);
        }

        var theme = await dbContext.GetOrCreateThemeAsync(specification.Theme.TopLevel, cancellationToken);

        var legoSet = request.ToLegoSet(Guid.CreateVersion7());
        legoSet.LinkToRebrickable(specification, theme.Id, timeProvider.GetUtcNow());

        dbContext.LegoSets.Add(legoSet);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/catalog/sets/{legoSet.Id}", SetResponse.From(legoSet));
    }
}