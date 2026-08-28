using BrickShare.Catalog.Api.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Respawn;

using Testcontainers.PostgreSql;

namespace BrickShare.Catalog.IntegrationTests;

/// <summary>
/// The Postgres every integration test runs against: one container for the whole test run,
/// created by the same migration that will run in the pipeline, and reset before each test.
/// </summary>
public sealed class CatalogDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("brickshare_catalog")
        .WithUsername("brickshare")
        .WithPassword("brickshare")
        .Build();

    private Respawner _respawner = null!;

    public CatalogApiFactory Api { get; private set; } = null!;
    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using CatalogDbContext context = NewDbContext();
        await context.Database.MigrateAsync();

        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],

            // Clear this and the database forgets it has a schema, so the next `dotnet ef`
            // command tries to apply InitialCatalog to a database that already has the table.
            TablesToIgnore = ["__EFMigrationsHistory"]
        });

        Api = new CatalogApiFactory(ConnectionString);
    }

    /// <summary>
    /// Empties every table, in an order the foreign keys allow. Called before each test.
    /// </summary>
    public async Task ResetAsync()
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);
    }

    /// <summary>
    /// A new context on the same database. Tests that need to see a row the way another
    /// connection would ask for two of these rather than clearing a change tracker.
    /// </summary>
    public CatalogDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
