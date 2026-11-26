using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class ThemeTests
{
    [Fact]
    public void FromRebrickable_creates_a_theme_from_the_top_level_theme()
    {
        var id = Guid.CreateVersion7();
        var starWars = new RebrickableTheme(1, "Star Wars", null);

        var theme = Theme.FromRebrickable(id, starWars);

        theme.Id.Should().Be(id);
        theme.Name.Should().Be("Star Wars");
        theme.RebrickableThemeId.Should().Be(1);
    }

    [Fact]
    public void FromRebrickable_rejects_a_sub_theme()
    {
        var starWars = new RebrickableTheme(1, "Star Wars", null);
        var episodeOne = new RebrickableTheme(2, "Episode I", starWars);

        Func<Theme> create = () => Theme.FromRebrickable(Guid.CreateVersion7(), episodeOne);

        create.Should().Throw<ArgumentException>();
    }
}