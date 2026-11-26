using System;

using BrickShare.Auth;
using BrickShare.Catalog.Api.Features.Access.Probe;
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
builder.Services.AddBrickShareAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddBrickShareAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapAccessProbe();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();

await app.RunAsync();