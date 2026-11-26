using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.Instances.Label;

public static class GetInstanceLabel
{
    public static void MapGetInstanceLabel(this RouteGroupBuilder instances) =>
        instances.MapGet("/{id:guid}/label", Handle);

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> Handle(
        Guid id,
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var view = await InstanceView.Query(dbContext, instance => instance.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (view is null)
        {
            return InstanceProblems.NotFound(id);
        }

        return TypedResults.Content(InstanceLabelRenderer.RenderLabel(view), "image/svg+xml");
    }
}