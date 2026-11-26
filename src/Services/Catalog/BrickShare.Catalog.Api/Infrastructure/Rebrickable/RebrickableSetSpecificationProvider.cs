using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Domain;

using Microsoft.Extensions.Caching.Memory;

using Polly.CircuitBreaker;
using Polly.Timeout;

namespace BrickShare.Catalog.Api.Infrastructure.Rebrickable;

public sealed class RebrickableSetSpecificationProvider(HttpClient httpClient, IMemoryCache cache) : ISetSpecificationProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<SetSpecification?> FindAsync(RebrickableSetNumber setNumber, CancellationToken cancellationToken)
    {
        try
        {
            return await FindFromRebrickableAsync(setNumber, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException)
        {
            throw new SetSpecificationUnavailableException("Rebrickable is unavailable.", exception);
        }
    }

    private async Task<SetSpecification?> FindFromRebrickableAsync(RebrickableSetNumber setNumber, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(setNumber.Value, out SetSpecification? cached))
        {
            return cached;
        }

        var setResponse = await GetAsync<SetResponse>($"api/v3/lego/sets/{setNumber.Value}/", cancellationToken);

        if (setResponse is null)
        {
            return null;
        }

        var minifigs = await GetRequiredAsync<MinifigsResponse>($"api/v3/lego/sets/{setNumber.Value}/minifigs/", cancellationToken);
        var theme = await LoadThemeAsync(setResponse.ThemeId, cancellationToken);

        var specification = new SetSpecification(
            setNumber,
            setResponse.Name,
            setResponse.Year,
            setResponse.NumParts,
            minifigs.Count,
            theme,
            setResponse.SetImgUrl is null ? null : new Uri(setResponse.SetImgUrl),
            new Uri(setResponse.SetUrl));

        cache.Set(setNumber.Value, specification, CacheDuration);

        return specification;
    }

    private async Task<RebrickableTheme> LoadThemeAsync(int themeId, CancellationToken cancellationToken)
    {
        var themeResponse = await GetRequiredAsync<ThemeResponse>($"api/v3/lego/themes/{themeId}/", cancellationToken);
        RebrickableTheme? parent = themeResponse.ParentId is { } parentId
            ? await LoadThemeAsync(parentId, cancellationToken)
            : null;

        return new RebrickableTheme(themeResponse.Id, themeResponse.Name, parent);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T> GetRequiredAsync<T>(string path, CancellationToken cancellationToken) where T : class =>
        await GetAsync<T>(path, cancellationToken)
        ?? throw new HttpRequestException($"Rebrickable has no resource at {path}.");

    private sealed record SetResponse(string Name, int? Year, int NumParts, int ThemeId, string? SetImgUrl, string SetUrl);

    private sealed record MinifigsResponse(int Count);

    private sealed record ThemeResponse(int Id, string Name, int? ParentId);
}