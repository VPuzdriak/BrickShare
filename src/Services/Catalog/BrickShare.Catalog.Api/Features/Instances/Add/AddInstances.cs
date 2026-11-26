using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Features.Sets;
using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Api.Infrastructure.Validation;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.Instances.Add;

public static class AddInstances
{
    public static void MapAddInstances(this RouteGroupBuilder sets) =>
        sets.MapPost("/{id:guid}/instances", Handle)
            .AddEndpointFilter<ValidationFilter<AddInstancesRequest>>();

    private static async Task<Results<Created<IReadOnlyList<InstanceResponse>>, ProblemHttpResult>> Handle(
        Guid id,
        AddInstancesRequest request,
        CatalogDbContext dbContext,
        IInventoryCodeSequence codeSequence,
        CancellationToken cancellationToken)
    {
        var setExists = await dbContext.LegoSets.AnyAsync(legoSet => legoSet.Id == id, cancellationToken);

        if (!setExists)
        {
            return SetProblems.NotFound(id);
        }

        var numbers = await codeSequence.NextValuesAsync(request.Quantity, cancellationToken);

        var instances = numbers
            .Select(number => new SetInstance(Guid.CreateVersion7(), id, InventoryCode.FromSequence(number), request.Condition))
            .ToList();

        dbContext.SetInstances.AddRange(instances);
        await dbContext.SaveChangesAsync(cancellationToken);

        IReadOnlyList<InstanceResponse> responses = instances.Select(InstanceResponse.From).ToList();

        return TypedResults.Created($"/catalog/sets/{id}/instances", responses);
    }
}