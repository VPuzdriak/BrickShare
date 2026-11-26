using BrickShare.Catalog.Api.Features.Themes.Facets;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Themes;

public static class ThemeEndpoints
{
    public static void MapThemeEndpoints(this IEndpointRouteBuilder app)
    {
        var themes = app.MapGroup("/catalog/themes")
            .AllowAnonymous();

        themes.MapGetThemeFacets();
    }
}