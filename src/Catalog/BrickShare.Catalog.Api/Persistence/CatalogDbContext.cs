using System.Reflection;

using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Copy> Copies => Set<Copy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every Money property in this model, forever, is numeric(10,2). Registered centrally so
        // the correctness does not depend on remembering it in each configuration file.
        configurationBuilder.Properties<Money>()
            .HaveConversion<MoneyConverter>()
            .HaveColumnType("numeric(10,2)");
    }
}
