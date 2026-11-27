using System;
using System.Threading.Tasks;

using BrickShare.Auth;
using BrickShare.Customers.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

using Testcontainers.PostgreSql;

using Xunit;

namespace BrickShare.Customers.Api.Tests;

public sealed class CustomerApiFactory : WebApplicationFactory<CustomersApi>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 27, 9, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("ConnectionStrings:customers", _postgres.GetConnectionString());
        builder.UseSetting("Authentication:Staff:Authority", CustomerTestTokens.StaffIssuer);
        builder.UseSetting("Authentication:Staff:Audience", CustomerTestTokens.ClientId);
        builder.UseSetting("Authentication:Customer:Authority", CustomerTestTokens.CustomerIssuer);
        builder.UseSetting("Authentication:Customer:Audience", CustomerTestTokens.ClientId);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);

            services.PostConfigure<JwtBearerOptions>(Schemes.Staff, options =>
                UseTestKey(options, CustomerTestTokens.StaffIssuer));

            services.PostConfigure<JwtBearerOptions>(Schemes.Customer, options =>
                UseTestKey(options, CustomerTestTokens.CustomerIssuer));
        });
    }

    private static void UseTestKey(JwtBearerOptions options, string issuer)
    {
        options.ConfigurationManager = null;
        options.TokenValidationParameters.ValidIssuer = issuer;
        options.TokenValidationParameters.ValidAudience = CustomerTestTokens.ClientId;
        options.TokenValidationParameters.IssuerSigningKey = CustomerTestTokens.SigningKey;
    }
}