using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class MinRentalDaysTests
{
    [Fact]
    public void MinRentalDays_keeps_the_number_of_days()
    {
        var minRentalDays = new MinRentalDays(3);

        minRentalDays.Days.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MinRentalDays_rejects_less_than_one_day(int days)
    {
        Func<MinRentalDays> create = () => new MinRentalDays(days);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }
}