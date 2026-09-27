using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class ThemeTests
{
    [Fact]
    public void An_adopted_theme_keeps_the_upstream_id_it_came_from()
    {
        Theme theme = Theme.Adopt(252, "Icons");

        Assert.Equal(252, theme.RebrickableId);
        Assert.Equal("Icons", theme.Name);
        Assert.NotEqual(Guid.Empty, theme.Id);
    }

    [Fact]
    public void A_theme_without_a_name_is_not_a_theme()
    {
        Assert.Throws<ArgumentException>(() => Theme.Adopt(252, "  "));
    }

    [Fact]
    public void A_theme_id_Rebrickable_could_not_have_issued_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Theme.Adopt(0, "Icons"));
    }
}
