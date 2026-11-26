using BrickShare.Auth;
using BrickShare.Catalog.Api.Features.Instances.Update;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Instances;

public static class InstanceEndpoints
{
    public static void MapInstanceEndpoints(this IEndpointRouteBuilder app)
    {
        var instances = app.MapGroup("/catalog/instances")
            .RequireAuthorization(Policies.AdminOnly);

        instances.MapUpdateInstance();
    }
}