using BrickShare.Catalog.Api.Data;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;
using BrickShare.Catalog.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.LegoThemes.Search;

internal sealed class SearchThemesHandler(CatalogDbContext dbContext) {
  public async Task<List<LegoThemeDto>> HandleAsync(string? searchTerm, CancellationToken cancellationToken) {
    IQueryable<LegoTheme> query = dbContext.LegoThemes;

    if (!string.IsNullOrEmpty(searchTerm)) {
      query = query.Where(t => t.Name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    var themes = await query.ToListAsync(cancellationToken);
    return themes.Select(t => new LegoThemeDto(t.Id, t.Name)).ToList();
  }
}
