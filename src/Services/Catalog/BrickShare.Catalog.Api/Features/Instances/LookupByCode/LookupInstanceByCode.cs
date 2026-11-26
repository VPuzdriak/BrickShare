using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.Instances.LookupByCode;

public static class LookupInstanceByCode
{
    public static void MapLookupInstanceByCode(this RouteGroupBuilder instances) =>
        instances.MapGet("/by-code/{inventoryCode}", Handle);

    private static async Task<Results<Ok<ScannedInstanceResponse>, ProblemHttpResult>> Handle(
        string inventoryCode,
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var parsed = InventoryCode.Parse(inventoryCode);

        if (parsed.IsFailure)
        {
            return InstanceProblems.InvalidInventoryCode(inventoryCode);
        }

        var view = await InstanceView.Query(dbContext, instance => instance.InventoryCode == parsed.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (view is null)
        {
            return InstanceProblems.UnknownInventoryCode(inventoryCode);
        }

        return TypedResults.Ok(ScannedInstanceResponse.From(view));
    }
}

public sealed record ScannedInstanceResponse(
    Guid Id,
    string InventoryCode,
    string Condition,
    string Status,
    Guid SetId,
    string SetName,
    string LegoSetNumber)
{
    public static ScannedInstanceResponse From(InstanceView view) => new(
        view.Id,
        view.InventoryCode.ToString(),
        view.Condition,
        view.Status,
        view.SetId,
        view.SetName,
        view.LegoSetNumber.Value);
}