using BrickShare.Auth;
using BrickShare.Catalog.Api.Features.Instances.Add;
using BrickShare.Catalog.Api.Features.Sets.Browse;
using BrickShare.Catalog.Api.Features.Sets.Create;
using BrickShare.Catalog.Api.Features.Sets.Deactivate;
using BrickShare.Catalog.Api.Features.Sets.Details;
using BrickShare.Catalog.Api.Features.Sets.ImageUpload;
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
        sets.MapAddInstances();
        sets.MapCreateImageUploadUrl();

        var publicSets = app.MapGroup("/catalog/sets")
            .AllowAnonymous();

        publicSets.MapBrowseSets();
        publicSets.MapGetSetDetails();
    }
}