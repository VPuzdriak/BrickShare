using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Infrastructure.Persistence;

public static class ThemeCatalog
{
    public static async Task<Theme> GetOrCreateThemeAsync(
        this CatalogDbContext dbContext, RebrickableTheme topLevelTheme, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Themes.FirstOrDefaultAsync(
            candidate => candidate.RebrickableThemeId == topLevelTheme.Id, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var theme = Theme.FromRebrickable(Guid.CreateVersion7(), topLevelTheme);

        dbContext.Themes.Add(theme);
        await dbContext.SaveChangesAsync(cancellationToken);

        return theme;
    }
}