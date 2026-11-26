using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.SharedKernel.Tests;

public class MoneyTests
{
    [Fact]
    public void Money_keeps_the_amount_and_the_currency()
    {
        var money = new Money(1250, "EUR");

        money.Amount.Should().Be(1250);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Money_rejects_a_negative_amount()
    {
        Func<Money> create = () => new Money(-1, "EUR");

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("eur")]
    [InlineData("EURO")]
    public void Money_rejects_a_currency_other_than_eur(string currency)
    {
        Func<Money> create = () => new Money(100, currency);

        create.Should().Throw<ArgumentException>();
    }
}