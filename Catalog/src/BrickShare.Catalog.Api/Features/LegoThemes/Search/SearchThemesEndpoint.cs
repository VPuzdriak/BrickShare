using Microsoft.AspNetCore.Mvc;

namespace BrickShare.Catalog.Api.Features.LegoThemes.Search;

internal static class SearchThemesEndpoint {
  internal const string Route = "/search";

  public static void MapSearchThemes(this RouteGroupBuilder group) {
    group.MapGet(Route, async (
      [AsParameters] SearchThemesRequest request,
      SearchThemesHandler handler,
      CancellationToken ct) => {
        var themes = await handler.HandleAsync(request.SearchTerm, ct);
        return Results.Ok(themes);
      });
  }
}

internal sealed record SearchThemesRequest(
  [FromQuery]
  string? SearchTerm);
