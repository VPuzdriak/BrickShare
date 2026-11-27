using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Customers.Api.Features.Customers.GetProfile;
using BrickShare.Customers.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Customers.Api.Features.Customers.UpdateProfile;

public static class UpdateProfile
{
    public static void MapUpdateProfile(this RouteGroupBuilder customers) =>
        customers.MapPut("/me", Handle);

    private static async Task<Results<Ok<CustomerProfileResponse>, ProblemHttpResult>> Handle(
        UpdateProfileRequest request,
        ClaimsPrincipal user,
        CustomerDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var customer = await CurrentCustomer.LoadOrCreateAsync(user, dbContext, timeProvider, cancellationToken);
        var result = customer.UpdateProfile(request.DisplayName, request.Phone);

        if (result.Error is { } error)
        {
            return CustomerProblems.InvalidProfile(error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(CustomerProfileResponse.From(customer));
    }
}