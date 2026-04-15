namespace BrickShare.Catalog.Api.Models;

internal sealed class LegoSet {
  public required Guid Id { get; init; }
  public required string Name { get; init; }
  public required string LegoId { get; init; }
  public required DateOnly ReleaseDate { get; init; }
  public required int NumberOfParts { get; init; }
  public required int AgeFrom { get; init; }
  public required Guid ThemeId { get; init; }
  public required LegoTheme Theme { get; init; }
}
