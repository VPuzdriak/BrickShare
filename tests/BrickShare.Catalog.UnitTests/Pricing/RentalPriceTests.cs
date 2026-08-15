using BrickShare.Catalog.Api.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

public class RentalPriceTests
{
    [Fact]
    public void RentalPrice_for_a_new_copy_is_the_base_price()
    {
        decimal price = PriceCalculator.RentalPrice(24.99m, ConditionGrade.New, Multipliers.Standard());

        Assert.Equal(24.99m, price);
    }

    [Fact]
    public void RentalPrice_for_an_excellent_copy_is_discounted_by_its_multiplier()
    {
        decimal price = PriceCalculator.RentalPrice(24.99m, ConditionGrade.Excellent, Multipliers.Standard());

        Assert.Equal(21.24m, price);
    }

    [Fact]
    public void RentalPrice_changes_when_an_admin_edits_the_multiplier()
    {
        GradeMultipliers before = new(new Dictionary<ConditionGrade, decimal>
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.85m,
            [ConditionGrade.Good] = 0.70m,
            [ConditionGrade.Fair] = 0.55m
        });

        GradeMultipliers after = new(new Dictionary<ConditionGrade, decimal>
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.90m,
            [ConditionGrade.Good] = 0.70m,
            [ConditionGrade.Fair] = 0.55m
        });

        Assert.Equal(21.24m, PriceCalculator.RentalPrice(24.99m, ConditionGrade.Excellent, before));
        Assert.Equal(22.49m, PriceCalculator.RentalPrice(24.99m, ConditionGrade.Excellent, after));
    }

    [Fact]
    public void RentalPrice_is_rounded_to_whole_cents()
    {
        // 24.99 × 0.55 = 13.7445, which is not an amount anyone can be charged.
        decimal price = PriceCalculator.RentalPrice(24.99m, ConditionGrade.Fair, Multipliers.Standard());

        Assert.Equal(13.74m, price);
    }

    [Fact]
    public void RentalPrice_rounds_a_half_cent_up_and_not_to_even()
    {
        // 17.75 × 0.70 = 12.425 exactly — a midpoint, and the one case where the
        // rounding mode is visible. Banker's rounding would give 12.42.
        decimal price = PriceCalculator.RentalPrice(17.75m, ConditionGrade.Good, Multipliers.Standard());

        Assert.Equal(12.43m, price);
    }

    [Theory]
    [InlineData(ConditionGrade.New, 24.99)]
    [InlineData(ConditionGrade.Excellent, 21.24)]
    [InlineData(ConditionGrade.Good, 17.49)]
    [InlineData(ConditionGrade.Fair, 13.74)]
    public void RentalPrice_applies_the_multiplier_for_each_grade(ConditionGrade grade, decimal expected)
    {
        decimal price = PriceCalculator.RentalPrice(24.99m, grade, Multipliers.Standard());

        Assert.Equal(expected, price);
    }
}
