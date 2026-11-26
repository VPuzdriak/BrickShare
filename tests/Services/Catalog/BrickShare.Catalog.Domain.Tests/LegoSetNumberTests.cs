using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class LegoSetNumberTests
{
    [Fact]
    public void LegoSetNumber_keeps_the_digits()
    {
        var number = new LegoSetNumber("10294");

        number.Value.Should().Be("10294");
    }

    [Fact]
    public void LegoSetNumber_rejects_an_empty_value()
    {
        Func<LegoSetNumber> create = () => new LegoSetNumber("");

        create.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("10294-1")]
    [InlineData("10294A")]
    public void LegoSetNumber_rejects_a_value_with_non_digits(string value)
    {
        Func<LegoSetNumber> create = () => new LegoSetNumber(value);

        create.Should().Throw<ArgumentException>();
    }
}