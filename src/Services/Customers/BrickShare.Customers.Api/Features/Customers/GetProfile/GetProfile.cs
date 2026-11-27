using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Customers.Api.Infrastructure.Identity;
using BrickShare.Customers.Api.Infrastructure.Persistence;
using BrickShare.Customers.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

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
        var customerId = user.CustomerId();

        var customer = await dbContext.Customers.FirstOrDefaultAsync(
            candidate => candidate.Id == customerId, cancellationToken);

        if (customer is null)
        {
            customer = new Customer(
                customerId,
                user.FindFirst("email")?.Value ?? string.Empty,
                user.FindFirst("name")?.Value ?? string.Empty,
                timeProvider.GetUtcNow());

            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(new CustomerProfileResponse(
            customer.Id, customer.Email, customer.DisplayName, customer.CreatedAt));
    }
}