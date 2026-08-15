using System.Diagnostics.CodeAnalysis;

namespace BrickShare.Catalog.Domain;

public sealed record SetNumber
{
    private const int MaxLength = 32;

    private SetNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static SetNumber Parse(string value) =>
        !TryParse(value, out SetNumber? setNumber)
            ? throw new FormatException($"'{value}' is not a usable LEGO set number.")
            : setNumber;

    public static bool TryParse(string? value, [NotNullWhen(true)] out SetNumber? setNumber)
    {
        setNumber = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length > MaxLength)
        {
            return false;
        }

        setNumber = new SetNumber(normalized);
        return true;
    }
}
