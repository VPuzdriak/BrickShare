using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyGradeTests
{
    [Fact]
    public void A_copy_is_registered_with_a_label_and_a_starting_grade()
    {
        Copy copy = ACopy.Graded(ConditionGrade.New, LabelCode.Parse("BRK-7F3K2Q"));

        Assert.Equal(ConditionGrade.New, copy.Grade);
        Assert.Equal("BRK-7F3K2Q", copy.Label.Value);
    }

    [Fact]
    public void A_copy_can_be_regraded_downward()
    {
        Copy copy = ACopy.Graded(ConditionGrade.New);

        copy.Regrade(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }

    [Fact]
    public void A_copy_cannot_be_regraded_upward()
    {
        Copy copy = ACopy.Graded(ConditionGrade.Fair);

        Assert.Throws<DomainRuleViolationException>(() => copy.Regrade(ConditionGrade.Good));
    }

    [Fact]
    public void Regrading_to_the_same_grade_is_allowed()
    {
        Copy copy = ACopy.Graded(ConditionGrade.Good);

        copy.Regrade(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }

    [Fact]
    public void A_copy_cannot_be_regraded_to_New()
    {
        Copy copy = ACopy.Graded(ConditionGrade.New);

        Assert.Throws<DomainRuleViolationException>(() => copy.Regrade(ConditionGrade.New));
    }

    [Fact]
    public void A_repaired_copy_can_have_its_grade_raised()
    {
        Copy copy = ACopy.Graded(ConditionGrade.Fair);

        copy.RaiseGradeAfterRepair(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }

    [Fact]
    public void A_repair_that_does_not_improve_the_grade_is_refused()
    {
        Copy copy = ACopy.Graded(ConditionGrade.Good);

        Assert.Throws<DomainRuleViolationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.Fair));
        Assert.Throws<DomainRuleViolationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.Good));
    }

    [Fact]
    public void A_repaired_copy_still_cannot_be_graded_New()
    {
        Copy copy = ACopy.Graded(ConditionGrade.Excellent);

        Assert.Throws<DomainRuleViolationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.New));
    }
}
