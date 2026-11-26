using System;

namespace BrickShare.SharedKernel;

public sealed record Money
{
    public Money(long amount, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (currency != "EUR")
        {
            throw new ArgumentException("Only EUR is supported.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public long Amount { get; }

    public string Currency { get; }
}