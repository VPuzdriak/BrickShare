namespace BrickShare.Catalog.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTest(CatalogDatabase database) : IAsyncLifetime
{
    protected CatalogDatabase Database { get; } = database;

    // Reset before, not after. See step 5.
    public Task InitializeAsync() => Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
