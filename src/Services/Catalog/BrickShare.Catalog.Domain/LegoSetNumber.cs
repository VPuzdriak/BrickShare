using System;
using System.Linq;

namespace BrickShare.Catalog.Domain;

public sealed record LegoSetNumber
{
    public LegoSetNumber(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        if (!value.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("A LEGO set number contains digits only.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}