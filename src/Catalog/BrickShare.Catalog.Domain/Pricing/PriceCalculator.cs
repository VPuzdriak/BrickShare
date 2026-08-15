namespace BrickShare.Catalog.Domain.Pricing;

public static class PriceCalculator
{
    public static Money RentalPrice(Money baseRentalPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseRentalPrice.Amount);
        ArgumentNullException.ThrowIfNull(multipliers);

        return baseRentalPrice * multipliers.For(grade);
    }

    public static Money DailyRate(
        Money baseRentalPrice,
        int minimumRentalDays,
        ConditionGrade grade,
        GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1);

        // Divides the rounded rental price on purpose: that is the number the customer was
        // quoted, so the daily rate has to be derived from it and not from a longer decimal
        // nobody ever saw.
        return RentalPrice(baseRentalPrice, grade, multipliers) / minimumRentalDays;
    }

    public static Money Deposit(Money retailPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retailPrice.Amount);
        ArgumentNullException.ThrowIfNull(multipliers);

        return retailPrice * multipliers.For(grade);
    }
}
