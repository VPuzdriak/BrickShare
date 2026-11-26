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

public class LegoSetPersistenceTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    [Fact]
    public async Task Saved_set_is_read_back_with_the_same_values()
    {
        var legoSet = CreateSet("10294");
        await SaveAsync(legoSet);

        var stored = (await LoadAsync(legoSet.Id)).Single();

        stored.Id.Should().Be(legoSet.Id);
        stored.LegoSetNumber.Should().Be(new LegoSetNumber("10294"));
        stored.MinRentalDays.Should().Be(new MinRentalDays(3));
        stored.DailyPrice.Should().Be(new Money(1500, "EUR"));
        stored.Deposit.Should().Be(new Money(15000, "EUR"));
        stored.ReplacementValue.Should().Be(new Money(30000, "EUR"));
        stored.LateFeePerDay.Should().Be(new Money(500, "EUR"));
    }

    [Fact]
    public async Task Two_sets_can_share_one_lego_set_number()
    {
        var first = CreateSet("10294");
        var second = CreateSet("10294");
        await SaveAsync(first, second);

        var stored = await LoadAsync(first.Id, second.Id);

        stored.Should().HaveCount(2);
    }

    private async Task SaveAsync(params LegoSet[] legoSets)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        dbContext.LegoSets.AddRange(legoSets);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<LegoSet[]> LoadAsync(params Guid[] ids)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        return await dbContext.LegoSets
            .Where(legoSet => ids.Contains(legoSet.Id))
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private static LegoSet CreateSet(string legoSetNumber) =>
        new(
            Guid.CreateVersion7(),
            new LegoSetNumber(legoSetNumber),
            new MinRentalDays(3),
            new Money(1500, "EUR"),
            new Money(15000, "EUR"),
            new Money(30000, "EUR"),
            new Money(500, "EUR"));
}