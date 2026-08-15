using System.Globalization;

namespace BrickShare.Catalog.Domain;

public readonly record struct Money
{
    private const int CurrencyDecimals = 2;

    public Money(decimal amount)
    {
        this.Amount = Math.Round(amount, CurrencyDecimals, MidpointRounding.AwayFromZero);
    }

    public decimal Amount { get; }

    public override string ToString() => Amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static readonly Money Zero = new(0m);

    public static Money operator *(Money amount, decimal factor) => new(amount.Amount * factor);

    public static Money operator /(Money amount, int divisor) => new(amount.Amount / divisor);
}
