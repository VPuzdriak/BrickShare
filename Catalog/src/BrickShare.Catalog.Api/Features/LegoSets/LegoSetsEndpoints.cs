using BrickShare.Catalog.Api.Features.LegoSets.Add;
using BrickShare.Catalog.Api.Features.LegoSets.Retrieve;
using BrickShare.Catalog.Api.Features.LegoSets.Search;

namespace BrickShare.Catalog.Api.Features.LegoSets;

internal static class LegoSetsEndpoints {
  internal const string RoutePrefix = "/lego-sets";

  public static void MapLegoSetsEndpoints(this RouteGroupBuilder group) {
    group.MapAddLegoSet();
    group.MapGetLegoSet();
    group.MapSearchLegoSets();
  }
}

