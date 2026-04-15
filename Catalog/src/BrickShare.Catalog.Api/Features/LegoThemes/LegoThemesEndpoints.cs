using BrickShare.Catalog.Api.Features.LegoThemes.Add;
using BrickShare.Catalog.Api.Features.LegoThemes.Retrieve;
using BrickShare.Catalog.Api.Features.LegoThemes.Search;

namespace BrickShare.Catalog.Api.Features.LegoThemes;

internal static class LegoThemesEndpoints {
  internal const string RoutePrefix = "/lego-themes";

  public static void MapLegoThemesEndpoints(this RouteGroupBuilder group) {
    group.MapGetThemes();
    group.MapAddTheme();
    group.MapSearchThemes();
  }
}
