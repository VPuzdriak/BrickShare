using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyIdentityTests
{
    [Fact]
    public void Two_registered_copies_have_different_identities()
    {
        Copy first = ACopy.Graded(ConditionGrade.New);
        Copy second = ACopy.Graded(ConditionGrade.New);

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void A_copy_on_the_shelf_has_no_retirement_date()
    {
        Copy copy = ACopy.Graded(ConditionGrade.New);

        Assert.Null(copy.RetiredAt);
    }

    [Fact]
    public void Retiring_a_copy_records_when_it_happened()
    {
        Copy copy = ACopy.Graded(ConditionGrade.New);
        DateTimeOffset when = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

        copy.Retire(when);

        Assert.Equal(when, copy.RetiredAt);
    }

    [Fact]
    public void A_copy_knows_which_set_it_is_a_copy_of()
    {
        Guid titanic = Guid.CreateVersion7();

        Copy copy = Copy.Register(titanic, LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New, 9200);

        Assert.Equal(titanic, copy.CatalogSetId);
        Assert.Equal(9200, copy.BaselineWeightInGrams);
    }

    [Fact]
    public void A_copy_belonging_to_no_set_is_not_a_copy_of_anything()
    {
        Assert.Throws<ArgumentException>(() =>
            Copy.Register(Guid.Empty, LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New, 9200));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_copy_that_was_never_weighed_is_not_registered(int grams)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Copy.Register(Guid.CreateVersion7(), LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New, grams));
    }
}
