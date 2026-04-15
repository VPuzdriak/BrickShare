namespace BrickShare.Catalog.Api.Shared;

internal sealed record PagedResult<T>(
  IReadOnlyList<T> Items,
  int TotalPages);

