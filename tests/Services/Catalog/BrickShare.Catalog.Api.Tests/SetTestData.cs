using System;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Domain;
using BrickShare.SharedKernel;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

public static class SetTestData
{
    public static LegoSet CreateSet(string legoSetNumber = "10294") =>
        new(
            Guid.CreateVersion7(),
            new LegoSetNumber(legoSetNumber),
            "Millennium Falcon",
            new MinRentalDays(3),
            new Money(1500, "EUR"),
            new Money(15000, "EUR"),
            new Money(30000, "EUR"),
            new Money(500, "EUR"));

    public static SetInstance CreateInstance(Guid setId) =>
        new(Guid.CreateVersion7(), setId, InventoryCode.FromSequence(1), InstanceCondition.New);

    public static async Task<LegoSet> SaveAsync(CatalogApiFactory factory, LegoSet legoSet)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        dbContext.LegoSets.Add(legoSet);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return legoSet;
    }

    public static async Task<LegoSet?> FindAsync(CatalogApiFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        return await dbContext.LegoSets.FindAsync([id], TestContext.Current.CancellationToken);
    }

    public static async Task<Theme?> FindThemeAsync(CatalogApiFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        return await dbContext.Themes.FindAsync([id], TestContext.Current.CancellationToken);
    }

    public static async Task<SetInstance> SaveInstanceAsync(CatalogApiFactory factory, SetInstance instance)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        dbContext.SetInstances.Add(instance);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return instance;
    }

    public static async Task<SetInstance?> FindInstanceAsync(CatalogApiFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        return await dbContext.SetInstances.FindAsync([id], TestContext.Current.CancellationToken);
    }
}