using BrickShare.Catalog.Domain;
using BrickShare.Catalog.Domain.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

internal static class Multipliers
{
    /// <summary>
    /// The multiplier table the pricing tests assume unless they are specifically about
    /// an admin editing one. Named so a failing test reads as "the standard table", and
    /// changed in exactly one place when the shop's numbers change.
    /// </summary>
    public static GradeMultipliers Standard()
    {
        return new GradeMultipliers(new Dictionary<ConditionGrade, decimal>
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.85m,
            [ConditionGrade.Good] = 0.70m,
            [ConditionGrade.Fair] = 0.55m
        });
    }
}
