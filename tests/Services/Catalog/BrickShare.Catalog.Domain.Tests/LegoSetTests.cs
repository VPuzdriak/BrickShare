using System;

using AwesomeAssertions;

using BrickShare.SharedKernel;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class LegoSetTests
{
    [Fact]
    public void LegoSet_keeps_its_number_rental_days_and_prices()
    {
        var id = Guid.CreateVersion7();

        var legoSet = new LegoSet(
            id,
            new LegoSetNumber("10294"),
            new MinRentalDays(3),
            new Money(1500, "EUR"),
            new Money(15000, "EUR"),
            new Money(30000, "EUR"),
            new Money(500, "EUR"));

        legoSet.Id.Should().Be(id);
        legoSet.LegoSetNumber.Should().Be(new LegoSetNumber("10294"));
        legoSet.MinRentalDays.Should().Be(new MinRentalDays(3));
        legoSet.DailyPrice.Should().Be(new Money(1500, "EUR"));
        legoSet.Deposit.Should().Be(new Money(15000, "EUR"));
        legoSet.ReplacementValue.Should().Be(new Money(30000, "EUR"));
        legoSet.LateFeePerDay.Should().Be(new Money(500, "EUR"));
    }
}