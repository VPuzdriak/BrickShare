using System;

using BrickShare.Catalog.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var catalogConnection = builder.Configuration.GetConnectionString("catalog")
    ?? throw new InvalidOperationException("The connection string 'catalog' is missing.");

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(catalogConnection));

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();

await app.RunAsync();