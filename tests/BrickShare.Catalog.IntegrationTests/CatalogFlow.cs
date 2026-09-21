using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;

namespace BrickShare.Catalog.IntegrationTests;

internal static class CatalogFlow
{
    public static async Task<Guid> LookUpTitanicAsync(this CatalogDatabase database, HttpClient client)
    {
        database.Rebrickable.Sets["10294-1"] = new
        {
            set_num = "10294-1",
            name = "Titanic",
            year = 2021,
            theme_id = 252,
            num_parts = 9092,
            set_img_url = "https://cdn.rebrickable.com/media/sets/10294-1.jpg"
        };

        database.Rebrickable.Themes[252] = new { id = 252, name = "Icons", parent_id = (int?)null };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/catalog/lookups", new { setNumber = "10294-1" });

        response.EnsureSuccessStatusCode();

        LookupResponse? draft = await response.Content.ReadFromJsonAsync<LookupResponse>();

        Assert.NotNull(draft);

        return draft.LookupId;
    }

    public static async Task<Guid> CatalogueTitanicAsync(this CatalogDatabase database, HttpClient client)
    {
        Guid lookupId = await database.LookUpTitanicAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/sets",
            new
            {
                lookupId,
                retailPrice = 629.99m,
                baseRentalPrice = 60.00m,
                minimumRentalDays = 7,
                minimumAge = 18
            });

        response.EnsureSuccessStatusCode();

        CatalogSetResponse? created = await response.Content.ReadFromJsonAsync<CatalogSetResponse>();

        Assert.NotNull(created);

        return created.Id;
    }
}
