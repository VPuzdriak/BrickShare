using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;

namespace BrickShare.Catalog.IntegrationTests;

internal static class CatalogFlow
{
    public static async Task<Guid> LookUpAsync(this CatalogDatabase database, HttpClient client, StockedSet stockedSet)
    {
        database.Rebrickable.Sets[stockedSet.SetNumber] = new
        {
            set_num = stockedSet.SetNumber,
            name = stockedSet.Name,
            year = stockedSet.Year,
            theme_id = stockedSet.ThemeId,
            num_parts = stockedSet.PieceCount,
            set_img_url = $"https://cdn.rebrickable.com/media/sets/{stockedSet.SetNumber}.jpg"
        };

        database.Rebrickable.Themes[stockedSet.ThemeId] = new
        {
            id = stockedSet.ThemeId, name = stockedSet.ThemeName, parent_id = (int?)null
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = stockedSet.SetNumber });

        response.EnsureSuccessStatusCode();

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        return draft.LookupId;
    }

    /// <summary>
    /// Any set, through the front door: Rebrickable stubbed, looked up, catalogued. The Titanic keeps
    /// its own helper because thirty tests already call it by name.
    /// </summary>
    public static async Task<Guid> CatalogueAsync(
        this CatalogDatabase database, HttpClient client, StockedSet stockedSet)
    {
        database.Rebrickable.Sets[stockedSet.SetNumber] = new
        {
            set_num = stockedSet.SetNumber,
            name = stockedSet.Name,
            year = stockedSet.Year,
            theme_id = stockedSet.ThemeId,
            num_parts = stockedSet.PieceCount,
            set_img_url = $"https://cdn.rebrickable.com/media/sets/{stockedSet.SetNumber}.jpg"
        };

        database.Rebrickable.Themes[stockedSet.ThemeId] =
            new { id = stockedSet.ThemeId, name = stockedSet.ThemeName, parent_id = (int?)null };

        HttpResponseMessage lookup = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = stockedSet.SetNumber });

        lookup.EnsureSuccessStatusCode();

        LookupResponse? draft = await lookup.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets",
            new
            {
                lookupId = draft.LookupId,
                retailPrice = stockedSet.RetailPrice,
                baseRentalPrice = stockedSet.BaseRentalPrice,
                minimumRentalDays = 7,
                minimumAge = stockedSet.MinimumAge
            });

        response.EnsureSuccessStatusCode();

        CatalogSetResponse? created = await response.Content.ReadFromJsonAsync<CatalogSetResponse>();

        Assert.NotNull(created);

        return created.Id;
    }

    public static async Task<Guid> RegisterCopyAsync(
        this CatalogDatabase database, HttpClient client, Guid setId, string grade)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/catalog/sets/{setId}/copies",
            new { copies = new[] { new { grade, baselineWeightInGrams = 1000 } } });

        response.EnsureSuccessStatusCode();

        RegisterCopiesResponse? registered =
            await response.Content.ReadFromJsonAsync<RegisterCopiesResponse>(database.Api.Json);

        Assert.NotNull(registered);

        return Assert.Single(registered.Copies).Id;
    }
}

internal sealed record StockedSet(
    string SetNumber,
    string Name,
    int Year,
    int ThemeId,
    string ThemeName,
    int PieceCount,
    decimal RetailPrice,
    decimal BaseRentalPrice,
    int MinimumAge)
{
    public static StockedSet Titanic { get; } =
        new("10294-1", "Titanic", 2021, 252, "Icons", 9092, 629.99m, 60.00m, 18);

    public static StockedSet Concorde { get; } =
        new("10318-1", "Concorde", 2023, 252, "Icons", 2083, 199.99m, 25.00m, 18);

    public static StockedSet MainStreetBuilding { get; } =
        new("41704-1", "Main Street Building", 2022, 494, "Friends", 1682, 129.99m, 15.00m, 8);
}
