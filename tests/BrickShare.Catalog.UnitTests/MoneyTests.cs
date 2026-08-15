using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Two_amounts_of_the_same_money_are_equal()
    {
        Money price = new(24.99m);

        Assert.Equal(new Money(24.99m), price);
    }

    [Fact]
    public void Money_is_rounded_to_whole_cents_when_it_is_created()
    {
        Money price = new(21.2415m);

        Assert.Equal(21.24m, price.Amount);
    }

    [Fact]
    public void Money_rounds_a_half_cent_away_from_zero()
    {
        Money price = new(12.425m);

        Assert.Equal(12.43m, price.Amount);
    }

    [Fact]
    public void Multiplying_money_keeps_it_in_whole_cents()
    {
        Money price = new Money(24.99m) * 0.85m;

        Assert.Equal(new Money(21.24m), price);
    }

    [Fact]
    public void Dividing_money_keeps_it_in_whole_cents()
    {
        Money dailyRate = new Money(21.24m) / 7;

        Assert.Equal(new Money(3.03m), dailyRate);
    }

    [Fact]
    public void The_default_value_of_money_is_zero()
    {
        Assert.Equal(Money.Zero, default);
    }

    [Fact]
    public void ToString_returns_the_amount_as_a_string_with_dot_as_separator()
    {
        Assert.Equal("24.99", new Money(24.99m).ToString());
    }
}
