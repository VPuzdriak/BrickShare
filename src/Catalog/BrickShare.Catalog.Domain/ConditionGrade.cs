namespace BrickShare.Catalog.Domain;

public enum ConditionGrade
{
    New,
    Excellent,
    Good,
    Fair
}

public static class ConditionGradeExtensions
{
    public static bool IsBetterThan(this ConditionGrade grade, ConditionGrade other)
    {
        // ConditionGrade is declared best to worst, so the LOWER enum value is the BETTER
        // grade. That inversion is the whole reason no comparison in this codebase is
        // written with < or > outside this method.
        return grade < other;
    }
}
