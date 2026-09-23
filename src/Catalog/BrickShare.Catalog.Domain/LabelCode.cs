using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace BrickShare.Catalog.Domain;

public sealed partial record LabelCode
{
    public const string Prefix = "BRK-";
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    public const int Length = 6;

    public static readonly int MaxLength = Prefix.Length + Length;

    private LabelCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static LabelCode Parse(string value)
    {
        if (!TryParse(value, out LabelCode? labelCode))
        {
            throw new FormatException($"'{value}' is not a BrickShare label code.");
        }

        return labelCode;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out LabelCode? labelCode)
    {
        labelCode = null;

        if (value is null)
        {
            return false;
        }

        string normalized = value.Trim().ToUpperInvariant();

        if (!Pattern().IsMatch(normalized))
        {
            return false;
        }

        labelCode = new LabelCode(normalized);
        return true;
    }

    public static LabelCode Mint() =>
        new($"{Prefix}{RandomNumberGenerator.GetString(Alphabet, Length)}");

    public override string ToString()
    {
        return Value;
    }

    // 0/O, 1/I/L and U are absent from the alphabet on purpose. This code is printed on a
    // box, scanned at a counter, and read down a phone when the scanner will not read it —
    // so the two characters people confuse most are not in it at all.
    [GeneratedRegex("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$")]
    private static partial Regex Pattern();
}
