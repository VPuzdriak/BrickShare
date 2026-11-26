using System;

using AwesomeAssertions;

using BrickShare.SharedKernel;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class LegoSetRebrickableTests
{
    private static readonly DateTimeOffset LookupTime = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LinkToRebrickable_stores_the_reference_the_facts_the_theme_and_the_lookup_time()
    {
        var legoSet = CreateSet();
        var themeId = Guid.CreateVersion7();

        legoSet.LinkToRebrickable(Titanic(), themeId, LookupTime);

        legoSet.RebrickableSetNum.Should().Be("10294-1");
        legoSet.Year.Should().Be(2021);
        legoSet.PieceCount.Should().Be(9092);
        legoSet.MinifigureCount.Should().Be(3);
        legoSet.ThemeId.Should().Be(themeId);
        legoSet.SpecsFetchedAt.Should().Be(LookupTime);
    }

    [Fact]
    public void LinkToRebrickable_keeps_the_name_the_admin_entered()
    {
        var legoSet = CreateSet();

        legoSet.LinkToRebrickable(Titanic(), Guid.CreateVersion7(), LookupTime);

        legoSet.Name.Should().Be("Millennium Falcon");
    }

    [Fact]
    public void RefreshSpecification_updates_the_rebrickable_facts_and_keeps_the_shop_fields()
    {
        var legoSet = CreateSet();
        legoSet.LinkToRebrickable(Titanic(), Guid.CreateVersion7(), LookupTime);
        var refreshedThemeId = Guid.CreateVersion7();
        var refreshed = Titanic() with { Name = "Titanic (2nd edition)", PieceCount = 9100 };

        legoSet.RefreshSpecification(refreshed, refreshedThemeId, LookupTime.AddDays(1));

        legoSet.Name.Should().Be("Titanic (2nd edition)");
        legoSet.PieceCount.Should().Be(9100);
        legoSet.ThemeId.Should().Be(refreshedThemeId);
        legoSet.SpecsFetchedAt.Should().Be(LookupTime.AddDays(1));
        legoSet.DailyPrice.Should().Be(new Money(1500, "EUR"));
        legoSet.MinRentalDays.Should().Be(new MinRentalDays(3));
        legoSet.IsActive.Should().BeTrue();
    }

    private static LegoSet CreateSet() =>
        new(
            Guid.CreateVersion7(),
            new LegoSetNumber("10294"),
            "Millennium Falcon",
            new MinRentalDays(3),
            new Money(1500, "EUR"),
            new Money(15000, "EUR"),
            new Money(30000, "EUR"),
            new Money(500, "EUR"));

    private static SetSpecification Titanic() => new(
        RebrickableSetNumber.Parse("10294-1").Value,
        "Titanic",
        2021,
        9092,
        3,
        new RebrickableTheme(158, "Top Theme", null),
        null,
        new Uri("https://rebrickable.com/sets/10294-1/titanic/"));
}