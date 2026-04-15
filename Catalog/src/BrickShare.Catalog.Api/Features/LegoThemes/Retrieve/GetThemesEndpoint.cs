namespace BrickShare.Catalog.Api.Features.LegoThemes.Retrieve;

internal static class GetThemesEndpoint {
  internal const string Route = "/";

  public static void MapGetThemes(this RouteGroupBuilder group) {
    group.MapGet(Route, async (GetThemesHandler handler, CancellationToken ct) => {
      var themes = await handler.HandleAsync(ct);
      return Results.Ok(themes);
    });
  }
}
