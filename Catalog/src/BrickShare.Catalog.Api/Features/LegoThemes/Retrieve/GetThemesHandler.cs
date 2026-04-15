using BrickShare.Catalog.Api.Data;
using BrickShare.Catalog.Api.Features.LegoThemes.Add;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.LegoThemes.Retrieve;

internal sealed class GetThemesHandler(CatalogDbContext dbContext) {
  public async Task<List<LegoThemeDto>> HandleAsync(CancellationToken cancellationToken) {
    var themes = await dbContext.LegoThemes.ToListAsync(cancellationToken);
    return themes.Select(t => new LegoThemeDto(t.Id, t.Name)).ToList();
  }
}
