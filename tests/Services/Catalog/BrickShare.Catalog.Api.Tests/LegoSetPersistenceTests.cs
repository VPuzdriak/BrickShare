using System;
using System.Linq;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Domain;
using BrickShare.SharedKernel;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class LegoSetPersistenceTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Saved_set_is_read_back_with_the_same_values()
    {
        var legoSet = SetTestData.CreateSet("10294");
        await SaveAsync(legoSet);

        var stored = (await LoadAsync(legoSet.Id)).Single();

        stored.Id.Should().Be(legoSet.Id);
        stored.LegoSetNumber.Should().Be(new LegoSetNumber("10294"));
        stored.Name.Should().Be("Millennium Falcon");
        stored.IsActive.Should().BeTrue();
        stored.MinRentalDays.Should().Be(new MinRentalDays(3));
        stored.DailyPrice.Should().Be(new Money(1500, "EUR"));
        stored.Deposit.Should().Be(new Money(15000, "EUR"));
        stored.ReplacementValue.Should().Be(new Money(30000, "EUR"));
        stored.LateFeePerDay.Should().Be(new Money(500, "EUR"));
    }

    [Fact]
    public async Task Two_sets_can_share_one_lego_set_number()
    {
        var first = SetTestData.CreateSet("10294");
        var second = SetTestData.CreateSet("10294");
        await SaveAsync(first, second);

        var stored = await LoadAsync(first.Id, second.Id);

        stored.Should().HaveCount(2);
    }

    [Fact]
    public async Task Linked_set_is_read_back_with_its_rebrickable_reference_and_theme()
    {
        var themeId = Guid.CreateVersion7();
        var legoSet = CreateLinkedSet(themeId);
        await SaveAsync(legoSet);

        var stored = (await LoadAsync(legoSet.Id)).Single();

        stored.RebrickableSetNum.Should().Be("10294-1");
        stored.Year.Should().Be(2021);
        stored.PieceCount.Should().Be(9092);
        stored.MinifigureCount.Should().Be(3);
        stored.ThemeId.Should().Be(themeId);
        stored.SpecsFetchedAt.Should().Be(new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Two_sets_cannot_share_one_rebrickable_reference()
    {
        var first = CreateLinkedSet(Guid.CreateVersion7());
        var second = CreateLinkedSet(Guid.CreateVersion7());

        Func<Task> save = () => SaveAsync(first, second);

        await save.Should().ThrowAsync<DbUpdateException>();
    }

    private static LegoSet CreateLinkedSet(Guid themeId)
    {
        var legoSet = SetTestData.CreateSet("10294");
        legoSet.LinkToRebrickable(
            SetSpecificationData.Titanic(),
            themeId,
            new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero));

        return legoSet;
    }

    private async Task SaveAsync(params LegoSet[] legoSets)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        dbContext.LegoSets.AddRange(legoSets);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<LegoSet[]> LoadAsync(params Guid[] ids)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        return await dbContext.LegoSets
            .Where(legoSet => ids.Contains(legoSet.Id))
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }
}