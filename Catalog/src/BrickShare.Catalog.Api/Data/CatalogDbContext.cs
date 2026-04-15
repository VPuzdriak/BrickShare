using BrickShare.Catalog.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Data;

internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options) {
  public required DbSet<LegoTheme> LegoThemes { get; init; }
  public required DbSet<LegoSet> LegoSets { get; init; }
}
