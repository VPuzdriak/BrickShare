using BrickShare.Auth;
using BrickShare.Catalog.Api.Features.Instances.Label;
using BrickShare.Catalog.Api.Features.Instances.LabelSheet;
using BrickShare.Catalog.Api.Features.Instances.LookupByCode;
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
        instances.MapGetInstanceLabel();
        instances.MapCreateLabelSheet();

        app.MapGroup("/catalog/instances")
            .RequireAuthorization(Policies.EmployeeOrAdmin)
            .MapLookupInstanceByCode();
    }
}