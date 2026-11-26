using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Instances.Update;

public static class UpdateInstance
{
    public static void MapUpdateInstance(this RouteGroupBuilder instances) =>
        instances.MapPut("/{id:guid}", Handle);

    private static async Task<Results<Ok<InstanceResponse>, ProblemHttpResult>> Handle(
        Guid id,
        UpdateInstanceRequest request,
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.SetInstances.FindAsync([id], cancellationToken);

        if (instance is null)
        {
            return InstanceProblems.NotFound(id);
        }

        instance.ChangeCondition(request.Condition);
        instance.ChangeStatus(request.Status);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(InstanceResponse.From(instance));
    }
}