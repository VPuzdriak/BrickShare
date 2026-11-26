using System;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Api.Infrastructure.Rebrickable;
using BrickShare.Catalog.Domain;

using Microsoft.Extensions.Caching.Memory;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

public class RebrickableSetSpecificationProviderTests
{
    [Fact]
    public async Task FindAsync_maps_the_set_with_its_minifigure_count_and_top_level_theme()
    {
        var provider = CreateProvider(RebrickableStub.WithTitanic());

        var specification = await provider.FindAsync(
            RebrickableSetNumber.Parse("10294").Value, TestContext.Current.CancellationToken);

        specification.Should().NotBeNull();
        specification.RebrickableSetNumber.Value.Should().Be("10294-1");
        specification.Name.Should().Be("Titanic");
        specification.Year.Should().Be(2021);
        specification.PieceCount.Should().Be(9092);
        specification.MinifigureCount.Should().Be(3);
        specification.Theme.TopLevel.Name.Should().Be("Top Theme");
        specification.PageUrl.Should().Be(new Uri("https://rebrickable.com/sets/10294-1/titanic/"));
    }

    [Fact]
    public async Task FindAsync_returns_null_when_rebrickable_does_not_know_the_set()
    {
        var provider = CreateProvider(new RebrickableStub());

        var specification = await provider.FindAsync(
            RebrickableSetNumber.Parse("99999").Value, TestContext.Current.CancellationToken);

        specification.Should().BeNull();
    }

    [Fact]
    public async Task FindAsync_answers_a_repeated_lookup_from_the_cache()
    {
        var stub = RebrickableStub.WithTitanic();
        var provider = CreateProvider(stub);
        var setNumber = RebrickableSetNumber.Parse("10294").Value;

        await provider.FindAsync(setNumber, TestContext.Current.CancellationToken);
        var requestsAfterFirstLookup = stub.RequestedPaths.Count;

        await provider.FindAsync(setNumber, TestContext.Current.CancellationToken);

        stub.RequestedPaths.Count.Should().Be(requestsAfterFirstLookup);
    }

    [Fact]
    public async Task FindAsync_raises_unavailable_when_rebrickable_fails()
    {
        var provider = CreateProvider(new RebrickableStub().Unavailable());

        Func<Task> act = () => provider.FindAsync(
            RebrickableSetNumber.Parse("10294").Value, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SetSpecificationUnavailableException>();
    }

    private static RebrickableSetSpecificationProvider CreateProvider(RebrickableStub stub) =>
        new(stub.CreateClient(), new MemoryCache(new MemoryCacheOptions()));
}