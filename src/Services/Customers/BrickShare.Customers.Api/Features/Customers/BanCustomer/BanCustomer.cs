using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Customers.Api.Infrastructure.Identity;
using BrickShare.Customers.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Customers.Api.Features.Customers.BanCustomer;

public static class BanCustomer
{
    public static void MapBanCustomer(this RouteGroupBuilder customers) =>
        customers.MapPost("/{id:guid}/bans", Handle);

    private static async Task<Results<Created<BanResponse>, ProblemHttpResult>> Handle(
        Guid id,
        BanCustomerRequest request,
        ClaimsPrincipal user,
        CustomerDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.FindAsync([id], cancellationToken);

        if (customer is null)
        {
            return CustomerProblems.CustomerNotFound(id);
        }

        var result = customer.AddBan(
            request.Reason,
            request.Comment,
            user.StaffId(),
            request.Until,
            timeProvider.GetUtcNow());

        if (result.Error is { } error)
        {
            return CustomerProblems.InvalidBan(error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var ban = result.Value;

        return TypedResults.Created($"/customers/{id}/bans/{ban.Id}", BanResponse.Map(ban));
    }
}