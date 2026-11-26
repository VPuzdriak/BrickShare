using System;
using System.Linq;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class InventoryCodeTests
{
    [Theory]
    [InlineData(1L, "HW95N4")]
    [InlineData(2L, "2NME94")]
    [InlineData(3L, "KHXMY3")]
    public void FromSequence_scrambles_the_number_and_appends_a_check_character(long number, string expected)
    {
        var code = InventoryCode.FromSequence(number);

        code.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(1_419_857L)]
    public void FromSequence_throws_when_the_number_is_out_of_range(long number)
    {
        var act = () => InventoryCode.FromSequence(number);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromSequence_gives_a_different_code_for_each_number()
    {
        var codes = Enumerable.Range(1, 1000)
            .Select(number => InventoryCode.FromSequence(number).Value)
            .ToHashSet();

        codes.Should().HaveCount(1000);
    }

    [Theory]
    [InlineData("W9X-25W", "W9X25W")]
    [InlineData("w9x-25w", "W9X25W")]
    [InlineData("W9X 25W", "W9X25W")]
    [InlineData("2222222", "2222222")]
    public void Parse_normalizes_case_dashes_and_spaces(string input, string expected)
    {
        var result = InventoryCode.Parse(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("W9X")]
    [InlineData("W9X25W9W")]
    [InlineData("W9X-20W")]
    [InlineData("O9X-25W")]
    [InlineData("W9X-25!")]
    [InlineData("W9X-25A")]
    [InlineData("W9Y-25W")]
    [InlineData("HW9-5N3")]
    public void Parse_fails_when_the_code_is_invalid(string input)
    {
        var result = InventoryCode.Parse(input);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ToString_prints_the_code_in_two_groups()
    {
        var code = InventoryCode.Parse("w9x25w").Value;

        code.ToString().Should().Be("W9X-25W");
    }
}