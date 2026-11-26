using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Api.Infrastructure.Validation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Sets.Create;

public static class CreateSet
{
    public static void MapCreateSet(this RouteGroupBuilder sets) =>
        sets.MapPost("/", Handle).AddEndpointFilter<ValidationFilter<SetRequest>>();

    private static async Task<Created<SetResponse>> Handle(
        SetRequest request, CatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        var legoSet = request.ToLegoSet(Guid.CreateVersion7());

        dbContext.LegoSets.Add(legoSet);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/catalog/sets/{legoSet.Id}", SetResponse.From(legoSet));
    }
}