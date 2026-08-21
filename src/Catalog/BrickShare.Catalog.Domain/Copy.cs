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
        Status = CopyStatus.Available;
    }

    public LabelCode Label { get; }

    public ConditionGrade Grade { get; private set; }

    public CopyStatus Status { get; private set; }

    # region Grage

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

    #endregion

    public static Copy Register(LabelCode label, ConditionGrade startingGrade)
    {
        ArgumentNullException.ThrowIfNull(label);

        return new Copy(label, startingGrade);
    }

    #region Status

    public void Reserve() => TransitionTo(CopyStatus.Reserved, CopyStatus.Available);

    public void ReleaseReservation() => TransitionTo(CopyStatus.Available, CopyStatus.Reserved);

    public void Collect() => TransitionTo(CopyStatus.OnRent, CopyStatus.Reserved);

    public void Return() => TransitionTo(CopyStatus.AwaitingInspection, CopyStatus.OnRent);

    public void BeginInspection() => TransitionTo(CopyStatus.InInspection, CopyStatus.AwaitingInspection);

    public void Shelve() => TransitionTo(CopyStatus.Available, CopyStatus.InInspection);

    public void SendForRepair() => TransitionTo(CopyStatus.InRepair, CopyStatus.InInspection);

    public void CompleteRepair() => TransitionTo(CopyStatus.Available, CopyStatus.InRepair);

    public void WriteOffAsLost() => TransitionTo(CopyStatus.Lost, CopyStatus.OnRent);

    public void Recover() => TransitionTo(CopyStatus.AwaitingInspection, CopyStatus.Lost);

    public void Retire() =>
        TransitionTo(CopyStatus.Retired,
            CopyStatus.Available, CopyStatus.InInspection, CopyStatus.InRepair);

    private void TransitionTo(CopyStatus to, params CopyStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            throw new InvalidOperationException(
                $"A copy cannot go from {Status} to {to}.");
        }

        Status = to;
    }

    #endregion
}
