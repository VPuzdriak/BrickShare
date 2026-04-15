using BrickShare.Catalog.Api.Data;
using BrickShare.Catalog.Api.Models;
using BrickShare.Catalog.Api.Shared;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.LegoSets.Search;

internal sealed class SearchLegoSetsHandler(CatalogDbContext dbContext) {
  public async Task<PagedResult<FoundLegoSetDto>> HandleAsync(
    string? searchTerm,
    string? legoId,
    Guid? themeId,
    int page,
    int pageSize,
    CancellationToken cancellationToken) {
    IQueryable<LegoSet> query = dbContext.LegoSets
      .AsNoTracking()
      .Include(s => s.Theme);

    if (!string.IsNullOrEmpty(searchTerm)) {
      query = query.Where(s => s.Name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrEmpty(legoId)) {
      query = query.Where(s => s.LegoId.StartsWith(legoId, StringComparison.OrdinalIgnoreCase));
    }

    if (themeId is not null) {
      query = query.Where(s => s.ThemeId == themeId);
    }

    var totalCount = await query.CountAsync(cancellationToken);
    var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

    var sets = await query
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .ToListAsync(cancellationToken);

    var items = sets
      .Select(s => new FoundLegoSetDto(s.Id, s.Name, s.LegoId, s.NumberOfParts, s.AgeFrom, s.Theme.Name))
      .ToList();

    return new PagedResult<FoundLegoSetDto>(items, totalPages);
  }
}
