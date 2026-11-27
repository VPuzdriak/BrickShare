using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Customers.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Customers.Api.Features.Customers.GetProfile;

public static class GetProfile
{
    public static void MapGetProfile(this RouteGroupBuilder customers) =>
        customers.MapGet("/me", Handle);

    private static async Task<Ok<CustomerProfileResponse>> Handle(
        ClaimsPrincipal user,
        CustomerDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var customer = await CurrentCustomer.LoadOrCreateAsync(user, dbContext, timeProvider, cancellationToken);

        return TypedResults.Ok(CustomerProfileResponse.From(customer));
    }
}