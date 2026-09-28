using System.Net.Http.Json;

using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.IntegrationTests.Browse;

public class ThemeListTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task The_filter_list_is_every_theme_that_has_a_set_in_it_by_name()
    {
        HttpClient client = Database.Api.CreateClient();

        await Database.CatalogueAsync(client, StockedSet.Titanic);
        await Database.CatalogueAsync(client, StockedSet.Concorde);
        await Database.CatalogueAsync(client, StockedSet.MainStreetBuilding);

        await using (CatalogDbContext dbContext = Database.NewDbContext())
        {
            dbContext.Themes.Add(Theme.Adopt(999, "Aardvarks"));
            await dbContext.SaveChangesAsync();
        }

        ThemesResponse? themes =
            await client.GetFromJsonAsync<ThemesResponse>("/api/v1/catalog/themes", Database.Api.Json);

        Assert.NotNull(themes);
        Assert.Equal(["Friends", "Icons"], themes.Themes.Select(theme => theme.Name));
    }
}
