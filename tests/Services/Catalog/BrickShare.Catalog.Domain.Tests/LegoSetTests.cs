using System;

using AwesomeAssertions;

using BrickShare.SharedKernel;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class LegoSetTests
{
    [Fact]
    public void LegoSet_keeps_its_details()
    {
        var id = Guid.CreateVersion7();

        var legoSet = new LegoSet(
            id,
            new LegoSetNumber("10294"),
            "Millennium Falcon",
            new MinRentalDays(3),
            new Money(1500, "EUR"),
            new Money(15000, "EUR"),
            new Money(30000, "EUR"),
            new Money(500, "EUR"));

        legoSet.Id.Should().Be(id);
        legoSet.LegoSetNumber.Should().Be(new LegoSetNumber("10294"));
        legoSet.Name.Should().Be("Millennium Falcon");
        legoSet.MinRentalDays.Should().Be(new MinRentalDays(3));
        legoSet.DailyPrice.Should().Be(new Money(1500, "EUR"));
        legoSet.Deposit.Should().Be(new Money(15000, "EUR"));
        legoSet.ReplacementValue.Should().Be(new Money(30000, "EUR"));
        legoSet.LateFeePerDay.Should().Be(new Money(500, "EUR"));
    }

    [Fact]
    public void New_set_is_active()
    {
        var legoSet = CreateSet();

        legoSet.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_replaces_the_details()
    {
        var legoSet = CreateSet();

        legoSet.Update(
            new LegoSetNumber("75192"),
            "Millennium Falcon UCS",
            new MinRentalDays(5),
            new Money(2500, "EUR"),
            new Money(25000, "EUR"),
            new Money(50000, "EUR"),
            new Money(800, "EUR"));

        legoSet.LegoSetNumber.Should().Be(new LegoSetNumber("75192"));
        legoSet.Name.Should().Be("Millennium Falcon UCS");
        legoSet.MinRentalDays.Should().Be(new MinRentalDays(5));
        legoSet.DailyPrice.Should().Be(new Money(2500, "EUR"));
        legoSet.LateFeePerDay.Should().Be(new Money(800, "EUR"));
    }

    [Fact]
    public void Deactivate_marks_the_set_inactive()
    {
        var legoSet = CreateSet();

        legoSet.Deactivate();

        legoSet.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_keeps_an_inactive_set_inactive()
    {
        var legoSet = CreateSet();

        legoSet.Deactivate();
        legoSet.Deactivate();

        legoSet.IsActive.Should().BeFalse();
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
}