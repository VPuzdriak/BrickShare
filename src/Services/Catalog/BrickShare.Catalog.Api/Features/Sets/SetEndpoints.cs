using BrickShare.Auth;
using BrickShare.Catalog.Api.Features.Sets.Create;
using BrickShare.Catalog.Api.Features.Sets.Deactivate;
using BrickShare.Catalog.Api.Features.Sets.Lookup;
using BrickShare.Catalog.Api.Features.Sets.RefreshSpecs;
using BrickShare.Catalog.Api.Features.Sets.Update;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Sets;

public static class SetEndpoints
{
    public static void MapSetEndpoints(this IEndpointRouteBuilder app)
    {
        var sets = app.MapGroup("/catalog/sets")
            .RequireAuthorization(Policies.AdminOnly);

        sets.MapCreateSet();
        sets.MapUpdateSet();
        sets.MapDeactivateSet();
        sets.MapLookupSet();
        sets.MapRefreshSetSpecs();
    }
}