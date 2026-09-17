using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Rebrickable;

public interface IRebrickableCatalog
{
    Task<RebrickableSet?> FindSetAsync(SetNumber number, CancellationToken cancellationToken);
    Task<RebrickableTheme?> FindThemeAsync(int themeId, CancellationToken cancellationToken);
}
