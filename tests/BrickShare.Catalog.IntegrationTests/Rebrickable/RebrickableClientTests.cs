using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Catalog.IntegrationTests.Rebrickable;

/// <summary>
/// No database, so no <see cref="DatabaseCollection"/>: this class runs in parallel with
/// everything else, and every test starts a stub server of its own.
/// </summary>
public sealed class RebrickableClientTests
{
    [Fact]
    public async Task A_known_set_comes_back_with_the_facts_staff_do_not_type()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();
        rebrickable.Sets["10294-1"] = Titanic();

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        RebrickableSet? set = await catalog.FindSetAsync(
            SetNumber.Parse("10294-1"), CancellationToken.None);

        Assert.NotNull(set);
        Assert.Equal("Titanic", set.Name);
        Assert.Equal(2021, set.Year);
        Assert.Equal(9092, set.PieceCount);
    }

    [Fact]
    public async Task A_set_Rebrickable_has_never_heard_of_is_not_found_rather_than_broken()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        RebrickableSet? set = await catalog.FindSetAsync(
            SetNumber.Parse("99999-1"), CancellationToken.None);

        Assert.Null(set);
    }

    [Fact]
    public async Task A_transient_failure_is_retried_rather_than_surfaced()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();
        rebrickable.Sets["10294-1"] = Titanic();
        rebrickable.FailNextRequests(2);

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        RebrickableSet? set = await catalog.FindSetAsync(
            SetNumber.Parse("10294-1"), CancellationToken.None);

        Assert.NotNull(set);
        Assert.Equal(3, rebrickable.Requests);
    }

    [Fact]
    public async Task A_theme_id_resolves_to_the_name_staff_will_see()
    {
        await using RebrickableStub rebrickable = await RebrickableStub.StartAsync();
        rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)null };

        await using ServiceProvider services = Services(rebrickable);
        var catalog = services.GetRequiredService<IRebrickableCatalog>();

        RebrickableTheme? theme = await catalog.FindThemeAsync(252, CancellationToken.None);

        Assert.NotNull(theme);
        Assert.Equal("Icons", theme.Name);
    }

    private static ServiceProvider Services(RebrickableStub rebrickable) =>
        new ServiceCollection()
            .AddRebrickable(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rebrickable:BaseAddress"] = rebrickable.BaseAddress,
                    ["Rebrickable:ApiKey"] = "test-key",
                    // Production waits two seconds before the first retry. A test that waits two
                    // seconds is a test somebody eventually deletes.
                    ["Rebrickable:Resilience:Retry:Delay"] = "00:00:00.001"
                })
                .Build())
            .BuildServiceProvider();

    private static object Titanic() => new
    {
        set_num = "10294-1",
        name = "Titanic",
        year = 2021,
        theme_id = 252,
        num_parts = 9092,
        set_img_url = "https://cdn.rebrickable.com/media/sets/10294-1.jpg"
    };
}
