namespace BrickShare.Catalog.Api.Pricing;

public static class PriceCalculator
{
    private const int CurrencyDecimals = 2;

    public static decimal RentalPrice(decimal baseRentalPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseRentalPrice);
        ArgumentNullException.ThrowIfNull(multipliers);

        return RoundToCents(baseRentalPrice * multipliers.For(grade));
    }

    public static decimal DailyRate(
        decimal baseRentalPrice,
        int minimumRentalDays,
        ConditionGrade grade,
        GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1);

        // Divides the rounded rental price on purpose: that is the number the customer was
        // quoted, so the daily rate has to be derived from it and not from a longer decimal
        // nobody ever saw.
        return RoundToCents(RentalPrice(baseRentalPrice, grade, multipliers) / minimumRentalDays);
    }

    public static decimal Deposit(decimal retailPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retailPrice);
        ArgumentNullException.ThrowIfNull(multipliers);

        return RoundToCents(retailPrice * multipliers.For(grade));
    }

    // Money is rounded away from zero, not to even. .NET's default is banker's rounding,
    // which is right for statistics and wrong for a price list — so the mode is always
    // written out rather than inherited.
    private static decimal RoundToCents(decimal amount)
    {
        return Math.Round(amount, CurrencyDecimals, MidpointRounding.AwayFromZero);
    }
}
