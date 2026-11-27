using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Customers.Api.Infrastructure.Persistence;
using BrickShare.Customers.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Customers.Api.Features.Customers.LiftBan;

public static class LiftBan
{
    public static void MapLiftBan(this RouteGroupBuilder customers) =>
        customers.MapDelete("/{id:guid}/bans/{banId:guid}", Handle);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        Guid id,
        Guid banId,
        CustomerDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.FindAsync([id], cancellationToken);

        if (customer is null)
        {
            return CustomerProblems.CustomerNotFound(id);
        }

        var result = customer.LiftBan(banId, timeProvider.GetUtcNow());

        if (result.Error is { } error)
        {
            return error == Customer.BanNotFound
                ? CustomerProblems.BanNotFound(banId)
                : CustomerProblems.BanAlreadyLifted(banId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}