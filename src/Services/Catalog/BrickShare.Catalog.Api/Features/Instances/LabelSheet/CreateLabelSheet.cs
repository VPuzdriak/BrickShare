using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Features.Instances.Label;
using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Api.Infrastructure.Validation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.Instances.LabelSheet;

public static class CreateLabelSheet
{
    public static void MapCreateLabelSheet(this RouteGroupBuilder instances) =>
        instances.MapPost("/labels", Handle)
            .AddEndpointFilter<ValidationFilter<LabelSheetRequest>>();

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> Handle(
        LabelSheetRequest request,
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var views = await InstanceView.Query(dbContext, instance => request.InstanceIds.Contains(instance.Id))
            .ToDictionaryAsync(view => view.Id, cancellationToken);

        if (request.InstanceIds.Any(id => !views.ContainsKey(id)))
        {
            return InstanceProblems.SomeNotFound();
        }

        var labels = request.InstanceIds.Select(id => views[id]).ToList();

        return TypedResults.Content(InstanceLabelRenderer.RenderSheet(labels), "image/svg+xml");
    }
}