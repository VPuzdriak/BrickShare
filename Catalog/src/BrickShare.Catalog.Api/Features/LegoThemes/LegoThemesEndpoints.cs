using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;
using BrickShare.Catalog.Api.Features.LegoThemes.Retrieve;

namespace BrickShare.Catalog.Api.Features.LegoThemes;

internal static class LegoThemesEndpoints {
  internal const string RoutePrefix = "/lego-themes";

  public static void MapLegoThemesEndpoints(this IEndpointRouteBuilder app) {
    var group = app.MapGroup(RoutePrefix)
      .AddEndpointFilter<ValidationEndpointFilter>();

    group.MapGetThemes();
    group.MapAddTheme();
  }
}
