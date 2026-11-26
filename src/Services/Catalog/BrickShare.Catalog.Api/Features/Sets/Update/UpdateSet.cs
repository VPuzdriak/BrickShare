using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Api.Infrastructure.Validation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Sets.Update;

public static class UpdateSet
{
    public static void MapUpdateSet(this RouteGroupBuilder sets) =>
        sets.MapPut("/{id:guid}", Handle).AddEndpointFilter<ValidationFilter<SetRequest>>();

    private static async Task<Results<Ok<SetResponse>, ProblemHttpResult>> Handle(
        Guid id, SetRequest request, CatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        var legoSet = await dbContext.LegoSets.FindAsync([id], cancellationToken);

        if (legoSet is null)
        {
            return SetProblems.NotFound(id);
        }

        request.ApplyTo(legoSet);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(SetResponse.From(legoSet));
    }
}