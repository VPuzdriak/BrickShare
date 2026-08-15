namespace BrickShare.Catalog.Domain;

/// <summary>
/// One physical box on the shelf. A catalog set is described once; the shop may own three
/// of them, and this is one of the three.
/// </summary>
public sealed class Copy
{
    private Copy(LabelCode label, ConditionGrade grade)
    {
        Label = label;
        Grade = grade;
    }

    public LabelCode Label { get; }

    public ConditionGrade Grade { get; private set; }

    public void Regrade(ConditionGrade newGrade)
    {
        if (newGrade == ConditionGrade.New)
        {
            throw new InvalidOperationException(
                "New is a starting grade only. A copy that has been out cannot be New again.");
        }

        if (newGrade.IsBetterThan(Grade))
        {
            throw new InvalidOperationException(
                $"A copy cannot be regraded from {Grade} up to {newGrade}. Grades only fall.");
        }

        Grade = newGrade;
    }

    public void RaiseGradeAfterRepair(ConditionGrade newGrade)
    {
        if (newGrade == ConditionGrade.New)
        {
            throw new InvalidOperationException(
                "New is a starting grade only. A repair can restore a copy, never its seal.");
        }

        if (!newGrade.IsBetterThan(Grade))
        {
            throw new InvalidOperationException(
                $"A repair must improve the grade. {newGrade} is not better than {Grade}.");
        }

        Grade = newGrade;
    }

    public static Copy Register(LabelCode label, ConditionGrade startingGrade)
    {
        ArgumentNullException.ThrowIfNull(label);

        return new Copy(label, startingGrade);
    }
}
