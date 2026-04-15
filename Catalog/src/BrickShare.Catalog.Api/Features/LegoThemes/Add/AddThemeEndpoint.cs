namespace BrickShare.Catalog.Api.Features.LegoThemes.Add;

internal static class AddThemeEndpoint {
  internal const string Route = "/add";

  public static void MapAddTheme(this RouteGroupBuilder group) {
    group.MapPost(Route, async (AddLegoThemeRequest request, AddThemeHandler handler, CancellationToken ct) => {
      var theme = await handler.HandleAsync(new AddTheme(request.Name), ct);
      var dto = new LegoThemeDto(theme.Id, theme.Name);
      return Results.Created($"{LegoThemesEndpoints.RoutePrefix}/{theme.Id}", dto);
    });
  }
}

internal sealed record AddLegoThemeRequest(string Name);

internal sealed record LegoThemeDto(Guid Id, string Name);
