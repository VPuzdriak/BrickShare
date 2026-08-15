using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyGradeTests
{
    [Fact]
    public void A_copy_is_registered_with_a_label_and_a_starting_grade()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        Assert.Equal(ConditionGrade.New, copy.Grade);
        Assert.Equal("BRK-7F3K2Q", copy.Label.Value);
    }

    [Fact]
    public void A_copy_can_be_regraded_downward()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        copy.Regrade(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }

    [Fact]
    public void A_copy_cannot_be_regraded_upward()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Fair);

        Assert.Throws<InvalidOperationException>(() => copy.Regrade(ConditionGrade.Good));
    }

    [Fact]
    public void Regrading_to_the_same_grade_is_allowed()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good);

        copy.Regrade(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }

    [Fact]
    public void A_copy_cannot_be_regraded_to_New()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        Assert.Throws<InvalidOperationException>(() => copy.Regrade(ConditionGrade.New));
    }

    [Fact]
    public void A_repaired_copy_can_have_its_grade_raised()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Fair);

        copy.RaiseGradeAfterRepair(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }

    [Fact]
    public void A_repair_that_does_not_improve_the_grade_is_refused()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good);

        Assert.Throws<InvalidOperationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.Fair));
        Assert.Throws<InvalidOperationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.Good));
    }

    [Fact]
    public void A_repaired_copy_still_cannot_be_graded_New()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Excellent);

        Assert.Throws<InvalidOperationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.New));
    }
}
