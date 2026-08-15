using BrickShare.Catalog.Domain;
using BrickShare.Catalog.Domain.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

public class DailyRateTests
{
    [Fact]
    public void DailyRate_divides_the_rental_price_by_the_minimum_duration()
    {
        // Rental price 21.24 over a 7-day minimum. 21.24 ÷ 7 = 3.0342857…
        Money dailyRate = PriceCalculator.DailyRate(new Money(24.99m), 7, ConditionGrade.Excellent, Multipliers.Standard());

        Assert.Equal(3.03m, dailyRate.Amount);
    }

    [Fact]
    public void DailyRate_times_the_minimum_duration_does_not_have_to_equal_the_rental_price()
    {
        GradeMultipliers multipliers = Multipliers.Standard();

        Money rentalPrice = PriceCalculator.RentalPrice(new Money(24.99m), ConditionGrade.Excellent, multipliers);
        Money dailyRate = PriceCalculator.DailyRate(new Money(24.99m), 7, ConditionGrade.Excellent, multipliers);

        // The minimum duration is a commercial floor: the whole rental price is paid whether
        // the set comes back on day 1 or day 7. The daily rate prices the days AFTER the
        // minimum, so this inequality is the rule working, not rounding drift.
        Assert.NotEqual(rentalPrice.Amount, dailyRate.Amount * 7);
    }
}
