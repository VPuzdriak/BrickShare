using System;
using System.Linq;

using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Domain;

public sealed record InventoryCode
{
    private const string Alphabet = "234579AEHKMNPTWXY";
    private const int Radix = 17;
    private const int CodeLength = 5;
    private const long Capacity = 1_419_857;
    private const long Multiplier = 738_457;

    private static readonly DomainError InvalidCode = new(
        "inventory_code.invalid",
        "An inventory code has 6 or 7 characters from the label alphabet and a valid check character.");

    private InventoryCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static InventoryCode FromSequence(long number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(number, Capacity);

        var scrambled = number * Multiplier % Capacity;
        var body = Encode(scrambled);

        return new InventoryCode(body + CheckCharacter(body));
    }

    public static Result<InventoryCode> Parse(string input)
    {
        var value = input.Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

        return IsValid(value)
            ? Result.Success(new InventoryCode(value))
            : Result.Failure<InventoryCode>(InvalidCode);
    }

    public override string ToString() => $"{Value[..3]}-{Value[3..]}";

    private static bool IsValid(string value) =>
        value.Length is 6 or 7
        && value.All(character => Alphabet.Contains(character))
        && CheckCharacter(value[..^1]) == value[^1];

    private static string Encode(long scrambled)
    {
        var digits = new char[CodeLength];
        var remaining = scrambled;

        for (var position = CodeLength - 1; position >= 0; position--)
        {
            digits[position] = Alphabet[(int)(remaining % Radix)];
            remaining /= Radix;
        }

        return new string(digits);
    }

    private static char CheckCharacter(string body)
    {
        var sum = 0;
        var factor = 2;

        for (var position = body.Length - 1; position >= 0; position--)
        {
            var addend = factor * Alphabet.IndexOf(body[position]);
            sum += addend / Radix + addend % Radix;
            factor = factor == 2 ? 1 : 2;
        }

        return Alphabet[(Radix - sum % Radix) % Radix];
    }
}