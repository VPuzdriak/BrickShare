using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<LegoSet> LegoSets => Set<LegoSet>();

    public DbSet<Theme> Themes => Set<Theme>();

    public DbSet<SetInstance> SetInstances => Set<SetInstance>();

    public DbSet<SetImage> SetImages => Set<SetImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasSequence<long>("inventory_code_seq");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}