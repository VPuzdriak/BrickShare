using Microsoft.AspNetCore.Mvc;

namespace BrickShare.Catalog.Api.Features.LegoSets.Search;

internal static class SearchLegoSetsEndpoint {
  internal const string Route = "/search";

  public static void MapSearchLegoSets(this RouteGroupBuilder group) {
    group.MapGet(Route, async (
      [AsParameters] SearchLegoSetsRequest request,
      SearchLegoSetsHandler handler,
      CancellationToken ct) => {
        var result = await handler.HandleAsync(request.SearchTerm, request.LegoId, request.ThemeId, request.Page, request.PageSize, ct);
        return Results.Ok(result);
      });
  }
}

internal sealed record SearchLegoSetsRequest(
  [FromQuery]
  string? SearchTerm,
  [FromQuery]
  string? LegoId,
  [FromQuery]
  Guid? ThemeId,
  [FromQuery]
  int Page = 1,
  [FromQuery]
  int PageSize = 10);

internal sealed record FoundLegoSetDto(
  Guid Id,
  string Name,
  string LegoId,
  int NumberOfParts,
  int AgeFrom,
  string ThemeName);
