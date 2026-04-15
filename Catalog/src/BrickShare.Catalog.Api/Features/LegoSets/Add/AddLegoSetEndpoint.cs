using ErrorOr;

namespace BrickShare.Catalog.Api.Features.LegoSets.Add;

internal static class AddLegoSetEndpoint {
  internal const string Route = "/add";

  public static void MapAddLegoSet(this RouteGroupBuilder group) {
    group.MapPost(Route, async (AddLegoSetRequest request, AddLegoSetHandler handler, CancellationToken ct) => {
      var result = await handler.HandleAsync(
        new AddLegoSet(request.Name, request.LegoId, request.ReleaseDate, request.NumberOfParts, request.AgeFrom, request.ThemeId),
        ct);

      if (result.IsError) {
        return result.FirstError.Type switch {
          ErrorType.Conflict => Results.Conflict(result.FirstError.Description),
          ErrorType.NotFound => Results.NotFound(result.FirstError.Description),
          _ => Results.Problem(result.FirstError.Description)
        };
      }

      var set = result.Value;
      var dto = new LegoSetDto(set.Id, set.Name, set.LegoId, set.ReleaseDate, set.NumberOfParts, set.AgeFrom, set.ThemeId, set.Theme.Name);
      return Results.Created($"{LegoSetsEndpoints.RoutePrefix}/{set.Id}", dto);
    });
  }
}

internal sealed record AddLegoSetRequest(
  string Name,
  string LegoId,
  DateOnly ReleaseDate,
  int NumberOfParts,
  int AgeFrom,
  Guid ThemeId);

internal sealed record LegoSetDto(
  Guid Id,
  string Name,
  string LegoId,
  DateOnly ReleaseDate,
  int NumberOfParts,
  int AgeFrom,
  Guid ThemeId,
  string ThemeName);
