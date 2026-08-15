using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class LabelCodeTests
{
    [Fact]
    public void A_set_number_is_trimmed_and_upper_cased()
    {
        Assert.Equal(LabelCode.Parse("BRK-7F3K2Q"), LabelCode.Parse(" brk-7f3k2q "));
    }

    [Fact]
    public void A_set_number_cannot_be_blank()
    {
        Assert.False(LabelCode.TryParse("   ", out _));
    }

    [Fact]
    public void Parse_throws_where_TryParse_returns_false()
    {
        Assert.Throws<FormatException>(() => LabelCode.Parse("   "));
    }

    [Fact]
    public void A_set_number_cannot_be_longer_than_a_set_number()
    {
        Assert.False(LabelCode.TryParse(new string('1', 100), out _));
    }

    [Fact]
    public void A_set_number_accepts_a_shape_we_do_not_recognise()
    {
        Assert.True(LabelCode.TryParse("brk-7f3k2q", out _));
    }

    [Fact]
    public void A_label_code_rejects_a_code_without_the_prefix()
    {
        Assert.False(LabelCode.TryParse("7F3K2Q", out _));
    }

    [Fact]
    public void A_label_code_rejects_an_ambiguous_character()
    {
        // The letter O. If this ever passes, someone has widened the alphabet and the
        // counter is about to start mis-keying codes.
        Assert.False(LabelCode.TryParse("BRK-7F3K2O", out _));
    }
}
