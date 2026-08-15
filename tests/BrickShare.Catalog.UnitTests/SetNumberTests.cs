using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class SetNumberTests
{
    [Fact]
    public void A_set_number_is_trimmed_and_upper_cased()
    {
        Assert.Equal(SetNumber.Parse("10294-1"), SetNumber.Parse(" 10294-1 "));
    }

    [Fact]
    public void A_set_number_cannot_be_blank()
    {
        Assert.False(SetNumber.TryParse("   ", out _));
    }

    [Fact]
    public void Parse_throws_where_TryParse_returns_false()
    {
        Assert.Throws<FormatException>(() => SetNumber.Parse("   "));
    }

    [Fact]
    public void A_set_number_cannot_be_longer_than_a_set_number()
    {
        Assert.False(SetNumber.TryParse(new string('1', 100), out _));
    }

    [Fact]
    public void A_set_number_accepts_a_shape_we_do_not_recognise()
    {
        // Deliberate. Rebrickable owns this format; the lookup in UC-1.1 is the real gate.
        Assert.True(SetNumber.TryParse("fig-001234", out _));
    }
}
