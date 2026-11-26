using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class RebrickableThemeTests
{
    [Fact]
    public void TopLevel_returns_the_parent_of_a_sub_theme()
    {
        var starWars = new RebrickableTheme(1, "Star Wars", null);
        var episodeOne = new RebrickableTheme(2, "Episode I", starWars);

        episodeOne.TopLevel.Should().Be(starWars);
    }

    [Fact]
    public void TopLevel_walks_up_to_the_root_of_a_nested_theme()
    {
        var starWars = new RebrickableTheme(1, "Star Wars", null);
        var episodeOne = new RebrickableTheme(2, "Episode I", starWars);
        var phantomMenace = new RebrickableTheme(3, "The Phantom Menace", episodeOne);

        phantomMenace.TopLevel.Should().Be(starWars);
    }
}