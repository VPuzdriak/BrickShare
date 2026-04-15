using BrickShare.Catalog.Api.Data;
using BrickShare.Catalog.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.LegoThemes.Add;

internal sealed record AddTheme(string Name);

internal sealed class AddThemeHandler(CatalogDbContext dbContext) {
  public async Task<LegoTheme> HandleAsync(AddTheme request, CancellationToken cancellationToken) {
    var list = await dbContext.LegoThemes.ToListAsync(cancellationToken);
    LegoTheme? theme = await dbContext
      .LegoThemes
      .AsNoTracking()
      .FirstOrDefaultAsync(t => t.Name
        .Equals(request.Name, StringComparison.OrdinalIgnoreCase), cancellationToken: cancellationToken);

    if (theme is not null) {
      return theme;
    }

    theme = new LegoTheme { Id = Guid.NewGuid(), Name = request.Name };

    await dbContext.AddAsync(theme, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    return theme;
  }
}
