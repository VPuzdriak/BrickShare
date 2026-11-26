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

    public static LegoSet CreateSpecifiedSet(
        string legoSetNumber, string name, int pieces, long dailyPrice, Guid themeId)
    {
        var legoSet = new LegoSet(
            Guid.CreateVersion7(),
            new LegoSetNumber(legoSetNumber),
            name,
            new MinRentalDays(3),
            new Money(dailyPrice, "EUR"),
            new Money(15000, "EUR"),
            new Money(30000, "EUR"),
            new Money(500, "EUR"));

        var specification = new SetSpecification(
            RebrickableSetNumber.Parse($"{legoSetNumber}-1").Value,
            name,
            2021,
            pieces,
            0,
            new RebrickableTheme(1, "Top Theme", null),
            null,
            new Uri($"https://rebrickable.com/sets/{legoSetNumber}-1/"));

        legoSet.LinkToRebrickable(specification, themeId, new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));

        return legoSet;
    }

    public static SetInstance CreateInstance(Guid setId, long sequence = 1) =>
        new(Guid.CreateVersion7(), setId, InventoryCode.FromSequence(sequence), InstanceCondition.New);

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

    public static async Task<Theme> SaveThemeAsync(CatalogApiFactory factory, Theme theme)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        dbContext.Themes.Add(theme);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return theme;
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

    public static async Task<SetImage> SaveImageAsync(CatalogApiFactory factory, SetImage image)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        dbContext.SetImages.Add(image);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return image;
    }

    public static async Task<SetImage?> FindImageAsync(CatalogApiFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        return await dbContext.SetImages.FindAsync([id], TestContext.Current.CancellationToken);
    }
}