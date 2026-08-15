namespace BrickShare.Catalog.Api.Pricing;

public sealed class GradeMultipliers
{
    private readonly Dictionary<ConditionGrade, decimal> _multipliers;

    public GradeMultipliers(IReadOnlyDictionary<ConditionGrade, decimal> multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);

        foreach (ConditionGrade grade in Enum.GetValues<ConditionGrade>())
        {
            if (!multipliers.TryGetValue(grade, out decimal multiplier))
            {
                throw new ArgumentException($"No multiplier configured for grade {grade}.", nameof(multipliers));
            }

            if (multiplier <= 0m)
            {
                throw new ArgumentException($"The multiplier for grade {grade} must be greater than zero.",
                    nameof(multipliers));
            }
        }

        // Copied, not held. The caller keeps a reference to the dictionary it passed in and
        // could otherwise change a price after this object was constructed and validated.
        _multipliers = multipliers.ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    public decimal For(ConditionGrade grade)
    {
        if (!_multipliers.TryGetValue(grade, out decimal multiplier))
        {
            throw new ArgumentOutOfRangeException(nameof(grade), grade, "No multiplier configured for this grade.");
        }

        return multiplier;
    }
}
