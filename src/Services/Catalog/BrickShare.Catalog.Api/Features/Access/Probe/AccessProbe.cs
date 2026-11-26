using BrickShare.Auth;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Access.Probe;

public static class AccessProbe
{
    public static void MapAccessProbe(this IEndpointRouteBuilder app)
    {
        var probe = app.MapGroup("/catalog/probe");

        probe.MapGroup("/admin")
            .RequireAuthorization(Policies.AdminOnly)
            .MapGet("/", () => TypedResults.Ok());

        probe.MapGroup("/staff")
            .RequireAuthorization(Policies.EmployeeOrAdmin)
            .MapGet("/", () => TypedResults.Ok());

        probe.MapGroup("/customer")
            .RequireAuthorization(Policies.CustomerOnly)
            .MapGet("/", () => TypedResults.Ok());
    }
}