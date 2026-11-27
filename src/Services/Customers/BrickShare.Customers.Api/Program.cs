using System;

using BrickShare.Auth;
using BrickShare.Customers.Api.Features.Customers;
using BrickShare.Customers.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var customersConnection = builder.Configuration.GetConnectionString("customers")
    ?? throw new InvalidOperationException("The connection string 'customers' is missing.");

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<CustomerDbContext>(options => options.UseNpgsql(customersConnection));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddBrickShareAuthentication(builder.Configuration);
builder.Services.AddBrickShareAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapCustomerEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Database.MigrateAsync();
}

await app.RunAsync();