using Azure.Core;
using Azure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using Npgsql;

namespace BrickShare.Catalog.Api.Persistence;

/// <summary>
/// How `dotnet ef` and the migration bundle build a <see cref="CatalogDbContext"/>. Not used by the
/// running application, which composes its own in Program.cs.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(
            configuration.GetConnectionString("Catalog"));

        // Same rule as Program.cs: a password means Compose, no password means Azure.
        if (string.IsNullOrEmpty(dataSourceBuilder.ConnectionStringBuilder.Password))
        {
            var credential = new DefaultAzureCredential();
            var request = new TokenRequestContext(
                ["https://ossrdbms-aad.database.windows.net/.default"]);

            dataSourceBuilder.UsePasswordProvider(
                // EF's migration commands are synchronous, so this is the one that gets called.
                // Blocking here costs nothing: this process exists to run one migration and exit.
                // GetToken is a real synchronous method, not an async one wearing a disguise.
                passwordProvider: _ => credential.GetToken(request, CancellationToken.None).Token,
                passwordProviderAsync: async (_, cancellationToken) =>
                    (await credential.GetTokenAsync(request, cancellationToken)).Token);
        }

        return new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>()
                .UseNpgsql(dataSourceBuilder.Build())
                .Options);
    }
}
