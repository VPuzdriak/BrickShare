using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace BrickShare.Catalog.Domain;

/// <summary>
/// The identity BrickShare issues to a physical box: "BRK-" plus six characters.
///
/// LEGO boxes carry no per-unit serial number, so a copy's identity has to be invented by
/// the shop and stuck on the outside (UC-1.2). Minting happens in episode 23; this type is
/// only the format.
/// </summary>
public sealed partial record LabelCode
{
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
