using BrickShare.Catalog.Api.Features.LegoSets.Add;

using ErrorOr;

namespace BrickShare.Catalog.Api.Features.LegoSets.Retrieve;

internal static class GetLegoSetEndpoint {
  internal const string Route = "/{id:guid}";

  public static void MapGetLegoSet(this RouteGroupBuilder group) {
    group.MapGet(Route, async (Guid id, GetLegoSetHandler handler, CancellationToken ct) => {
      var result = await handler.HandleAsync(id, ct);

      if (result.IsError) {
        return result.FirstError.Type switch {
          ErrorType.NotFound => Results.NotFound(result.FirstError.Description),
          _ => Results.Problem(result.FirstError.Description)
        };
      }

      var set = result.Value;
      var dto = new LegoSetDto(set.Id, set.Name, set.LegoId, set.ReleaseDate, set.NumberOfParts, set.AgeFrom, set.ThemeId, set.Theme.Name);
      return Results.Ok(dto);
    });
  }
}
