using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Sets.RefreshSpecs;

public static class RefreshSetSpecs
{
    public static void MapRefreshSetSpecs(this RouteGroupBuilder sets) =>
        sets.MapPost("/{id:guid}/refresh-specs", Handle);

    private static async Task<Results<Ok<SetResponse>, ProblemHttpResult>> Handle(
        Guid id,
        CatalogDbContext dbContext,
        ISetSpecificationProvider specifications,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var legoSet = await dbContext.LegoSets.FindAsync([id], cancellationToken);

        if (legoSet is null)
        {
            return SetProblems.NotFound(id);
        }

        if (legoSet.RebrickableSetNum is null)
        {
            return SetProblems.NoRebrickableReference(id);
        }

        var setNumber = RebrickableSetNumber.Parse(legoSet.RebrickableSetNum).Value;
        SetSpecification? specification;

        try
        {
            specification = await specifications.FindAsync(setNumber, cancellationToken);
        }
        catch (SetSpecificationUnavailableException)
        {
            return SetProblems.RebrickableUnavailable();
        }

        if (specification is null)
        {
            return SetProblems.UnknownSetNumber(setNumber.Value);
        }

        var theme = await dbContext.GetOrCreateThemeAsync(specification.Theme.TopLevel, cancellationToken);

        legoSet.RefreshSpecification(specification, theme.Id, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(SetResponse.From(legoSet));
    }
}