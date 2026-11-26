using System.Linq;

using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Domain;

public sealed record RebrickableSetNumber
{
    private RebrickableSetNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public LegoSetNumber LegoSetNumber => new(Value[..Value.IndexOf('-')]);

    private const string DefaultVariant = "-1";

    public static Result<RebrickableSetNumber> Parse(string input)
    {
        var parts = input.Split('-');

        if (parts.Length is not (1 or 2) || parts.Any(part => part.Length == 0 || !part.All(char.IsAsciiDigit)))
        {
            return Result.Failure<RebrickableSetNumber>(new DomainError(
                "set_number.invalid",
                "A Rebrickable set number is digits, optionally followed by a dash and a variant number."));
        }

        var value = parts.Length == 1 ? input + DefaultVariant : input;

        return Result.Success(new RebrickableSetNumber(value));
    }
}