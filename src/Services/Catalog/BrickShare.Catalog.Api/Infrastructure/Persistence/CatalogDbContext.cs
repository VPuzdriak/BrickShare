using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<LegoSet> LegoSets => Set<LegoSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}