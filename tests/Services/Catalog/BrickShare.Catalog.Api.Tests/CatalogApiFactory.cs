using System;
using System.Threading.Tasks;

using BrickShare.Auth;
using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

using Npgsql;

using Respawn;

using Testcontainers.PostgreSql;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

public sealed class CatalogApiFactory : WebApplicationFactory<CatalogApi>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();
    private Respawner _respawner = null!;

    public FakeSetSpecificationProvider SetSpecifications { get; } = new();

    public FakeSetImageStorage SetImageStorage { get; } = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);

        ((MemoryCache)Services.GetRequiredService<IMemoryCache>()).Compact(1.0);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("ConnectionStrings:catalog", _postgres.GetConnectionString());
        builder.UseSetting("Rebrickable:ApiKey", "test-key");
        builder.UseSetting("Authentication:Staff:Authority", EntraTestTokens.StaffIssuer);
        builder.UseSetting("Authentication:Staff:Audience", EntraTestTokens.ClientId);
        builder.UseSetting("Authentication:Customer:Authority", EntraTestTokens.CustomerIssuer);
        builder.UseSetting("Authentication:Customer:Audience", EntraTestTokens.ClientId);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ISetSpecificationProvider>(SetSpecifications);
            services.AddSingleton<ISetImageStorage>(SetImageStorage);
            services.AddSingleton<TimeProvider>(Time);

            services.PostConfigure<JwtBearerOptions>(Schemes.Staff, options =>
                UseEntraTestKey(options, EntraTestTokens.StaffIssuer));

            services.PostConfigure<JwtBearerOptions>(Schemes.Customer, options =>
                UseEntraTestKey(options, EntraTestTokens.CustomerIssuer));
        });
    }

    private static void UseEntraTestKey(JwtBearerOptions options, string issuer)
    {
        options.ConfigurationManager = null;
        options.TokenValidationParameters.ValidIssuer = issuer;
        options.TokenValidationParameters.ValidAudience = EntraTestTokens.ClientId;
        options.TokenValidationParameters.IssuerSigningKey = EntraTestTokens.SigningKey;
    }
}