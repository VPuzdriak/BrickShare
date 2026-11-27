using BrickShare.Auth;
using BrickShare.Customers.Api.Features.Customers.GetProfile;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Customers.Api.Features.Customers;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/customers")
            .RequireAuthorization(Policies.CustomerOnly);

        customers.MapGetProfile();
    }
}