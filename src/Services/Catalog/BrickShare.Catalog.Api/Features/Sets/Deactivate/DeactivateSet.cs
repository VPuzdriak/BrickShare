using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Sets.Deactivate;

public static class DeactivateSet
{
    public static void MapDeactivateSet(this RouteGroupBuilder sets) =>
        sets.MapDelete("/{id:guid}", Handle);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        Guid id, CatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        var legoSet = await dbContext.LegoSets.FindAsync([id], cancellationToken);

        if (legoSet is null)
        {
            return SetProblems.NotFound(id);
        }

        legoSet.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}