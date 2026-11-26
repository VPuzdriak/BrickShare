using System;

using BrickShare.Auth;
using BrickShare.Catalog.Api.Features.Access.Probe;
using BrickShare.Catalog.Api.Features.Instances;
using BrickShare.Catalog.Api.Features.Sets;
using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Api.Infrastructure.Rebrickable;
using BrickShare.Catalog.Api.Infrastructure.Storage;
using BrickShare.Catalog.Domain;

using FluentValidation;

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
builder.Services.AddProblemDetails();
builder.Services.AddValidatorsFromAssemblyContaining<SetRequestValidator>();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(catalogConnection));
builder.Services.AddScoped<IInventoryCodeSequence, PostgresInventoryCodeSequence>();
builder.Services.AddMemoryCache();
builder.Services.AddRebrickableLookup(builder.Configuration);
builder.Services.AddSetImageStorage(builder.Configuration);
builder.Services.AddBrickShareAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddBrickShareAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapAccessProbe();
app.MapSetEndpoints();
app.MapInstanceEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();

await app.RunAsync();