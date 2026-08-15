using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class ConditionGradeTests
{
    [Fact]
    public void New_is_a_better_grade_than_Excellent()
    {
        Assert.True(ConditionGrade.New.IsBetterThan(ConditionGrade.Excellent));
    }

    [Fact]
    public void A_grade_is_not_better_than_itself()
    {
        Assert.False(ConditionGrade.Good.IsBetterThan(ConditionGrade.Good));
    }
}
