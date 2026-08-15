using BrickShare.Catalog.Api.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

public class PriceCalculatorGuardTests
{
    [Fact]
    public void DailyRate_rejects_a_minimum_duration_of_zero_days()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PriceCalculator.DailyRate(24.99m, 0, ConditionGrade.New, Multipliers.Standard()));
    }

    [Fact]
    public void RentalPrice_rejects_a_negative_base_price()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PriceCalculator.RentalPrice(-1.00m, ConditionGrade.New, Multipliers.Standard()));
    }

    [Fact]
    public void Deposit_rejects_a_negative_retail_price()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PriceCalculator.Deposit(-1.00m, ConditionGrade.New, Multipliers.Standard()));
    }

    [Fact]
    public void GradeMultipliers_rejects_a_table_missing_a_grade()
    {
        Dictionary<ConditionGrade, decimal> incomplete = new()
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.85m,
            [ConditionGrade.Good] = 0.70m
        };

        Assert.Throws<ArgumentException>(() => new GradeMultipliers(incomplete));
    }
}
