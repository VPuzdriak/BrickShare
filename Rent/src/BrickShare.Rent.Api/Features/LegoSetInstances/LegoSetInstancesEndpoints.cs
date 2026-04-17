using BrickShare.Rent.Api.Features.LegoSetInstances.Add;

namespace BrickShare.Rent.Api.Features.LegoSetInstances;

internal static class LegoSetInstancesEndpoints {
  internal const string RoutePrefix = "/lego-sets";

  public static void MapLegoSetInstancesEndpoints(this RouteGroupBuilder group) {
    group.MapAddLegoSetInstance();
  }
}
