using System.Net;

using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Rebrickable;

internal sealed class RebrickableClient(HttpClient http) : IRebrickableCatalog
{
    public async Task<RebrickableSet?> FindSetAsync(
        SetNumber number, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(
            $"lego/sets/{Uri.EscapeDataString(number.Value)}/", cancellationToken);

        // A typo is the most likely thing a staff member does at this endpoint, and it must not
        // arrive at the handler looking like a Rebrickable outage. Those two need different words
        // on screen, so they get different return values here.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        RebrickableSetPayload payload =
            await response.Content.ReadFromJsonAsync<RebrickableSetPayload>(cancellationToken)
            ?? throw new InvalidOperationException($"Rebrickable sent an empty body for set {number}.");

        return new RebrickableSet(
            SetNumber.Parse(payload.SetNumber),
            payload.Name,
            payload.Year,
            payload.ThemeId,
            payload.PieceCount,
            payload.ImageUrl);
    }

    public async Task<RebrickableTheme?> FindThemeAsync(int themeId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(
            $"lego/themes/{themeId}/", cancellationToken);

        // Null here does not mean the same thing as null from FindSetAsync. A set 404 is a staff
        // typo; a theme 404 is Rebrickable contradicting itself, because the id came from
        // Rebrickable one call ago. The handler treats the two differently. See step 6.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        RebrickableThemePayload payload =
            await response.Content.ReadFromJsonAsync<RebrickableThemePayload>(cancellationToken)
            ?? throw new InvalidOperationException($"Rebrickable sent an empty body for theme {themeId}.");

        return new RebrickableTheme(payload.Id, payload.Name);
    }
}
