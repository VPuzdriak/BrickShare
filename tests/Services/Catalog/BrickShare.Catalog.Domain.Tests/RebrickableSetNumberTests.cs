using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class RebrickableSetNumberTests
{
    [Fact]
    public void Without_a_variant_the_first_variant_is_used()
    {
        var result = RebrickableSetNumber.Parse("10294");

        result.Value.Value.Should().Be("10294-1");
    }

    [Fact]
    public void An_explicit_variant_is_kept()
    {
        var result = RebrickableSetNumber.Parse("10294-2");

        result.Value.Value.Should().Be("10294-2");
    }

    [Fact]
    public void The_LEGO_set_number_is_the_part_before_the_variant()
    {
        var setNumber = RebrickableSetNumber.Parse("10294-2").Value;

        setNumber.LegoSetNumber.Value.Should().Be("10294");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("10294-")]
    [InlineData("-1")]
    [InlineData("10294-1-2")]
    public void Parse_fails_for_text_that_is_not_a_set_number(string input)
    {
        var result = RebrickableSetNumber.Parse(input);

        result.IsFailure.Should().BeTrue();
    }
}