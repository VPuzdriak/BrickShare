using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyIdentityTests
{
    [Fact]
    public void Two_registered_copies_have_different_identities()
    {
        Copy first = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);
        Copy second = Copy.Register(LabelCode.Parse("BRK-9H4M2N"), ConditionGrade.New);

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void A_copy_on_the_shelf_has_no_retirement_date()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        Assert.Null(copy.RetiredAt);
    }

    [Fact]
    public void Retiring_a_copy_records_when_it_happened()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);
        DateTimeOffset when = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

        copy.Retire(when);

        Assert.Equal(when, copy.RetiredAt);
    }
}
