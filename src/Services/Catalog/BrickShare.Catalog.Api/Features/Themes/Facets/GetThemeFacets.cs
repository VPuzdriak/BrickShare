using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BrickShare.Catalog.Api.Features.Themes.Facets;

public static class GetThemeFacets
{
    private const string CacheKey = "catalog:theme-facets";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public static void MapGetThemeFacets(this RouteGroupBuilder themes) =>
        themes.MapGet("/", Handle);

    private static async Task<Ok<List<ThemeFacetResponse>>> Handle(
        CatalogDbContext dbContext,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        var facets = await cache.GetOrCreateAsync(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return CountActiveSetsAsync(dbContext, cancellationToken);
        });

        return TypedResults.Ok(facets!);
    }

    private static Task<List<ThemeFacetResponse>> CountActiveSetsAsync(
        CatalogDbContext dbContext, CancellationToken cancellationToken) =>
        dbContext.Themes
            .OrderBy(theme => theme.Name)
            .ThenBy(theme => theme.Id)
            .Select(theme => new ThemeFacetResponse(
                theme.Id,
                theme.Name,
                dbContext.LegoSets.Count(legoSet => legoSet.IsActive && legoSet.ThemeId == theme.Id)))
            .ToListAsync(cancellationToken);
}