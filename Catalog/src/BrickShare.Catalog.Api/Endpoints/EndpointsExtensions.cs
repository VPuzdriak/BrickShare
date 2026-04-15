using BrickShare.Catalog.Api.Features.LegoThemes;

namespace BrickShare.Catalog.Api.Endpoints;

internal static class EndpointsExtensions {
  public static void MapEndpoints(this IEndpointRouteBuilder app) {
    app.MapLegoThemesEndpoints();
  }
}
