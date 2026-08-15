using BrickShare.Catalog.Api.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

public class DepositTests
{
    [Fact]
    public void Deposit_for_a_new_copy_is_the_full_retail_price()
    {
        decimal deposit = PriceCalculator.Deposit(199.99m, ConditionGrade.New, Multipliers.Standard());

        Assert.Equal(199.99m, deposit);
    }

    [Fact]
    public void Deposit_for_a_worn_copy_is_reduced_by_the_same_multiplier_as_the_rental_price()
    {
        // 199.99 × 0.55 = 109.99450 → 109.99
        decimal deposit = PriceCalculator.Deposit(199.99m, ConditionGrade.Fair, Multipliers.Standard());

        Assert.Equal(109.99m, deposit);
    }
}
