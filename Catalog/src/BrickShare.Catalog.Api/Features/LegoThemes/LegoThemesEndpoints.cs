using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;

namespace BrickShare.Catalog.Api.Features.LegoThemes;

internal static class LegoThemesEndpoints {
  internal const string RoutePrefix = "/lego-themes";

  public static void MapLegoThemesEndpoints(this IEndpointRouteBuilder app) {
    var group = app.MapGroup(RoutePrefix)
      .AddEndpointFilter<ValidationEndpointFilter>();

    group.MapAddTheme();
  }
}
