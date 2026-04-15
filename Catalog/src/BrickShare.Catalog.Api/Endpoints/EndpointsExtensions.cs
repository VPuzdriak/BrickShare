using BrickShare.Catalog.Api.Features.LegoSets;
using BrickShare.Catalog.Api.Features.LegoThemes;

namespace BrickShare.Catalog.Api.Endpoints;

internal static class EndpointsExtensions {
  public static void MapEndpoints(this IEndpointRouteBuilder app) {
    app.MapGroup(LegoThemesEndpoints.RoutePrefix)
      .WithTags("Lego Themes")
      .AddEndpointFilter<ValidationEndpointFilter>()
      .MapLegoThemesEndpoints();

    app.MapGroup(LegoSetsEndpoints.RoutePrefix)
      .WithTags("Lego Sets")
      .AddEndpointFilter<ValidationEndpointFilter>()
      .MapLegoSetsEndpoints();
  }
}
