using BrickShare.Catalog.Api.Data;
using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Features.LegoThemes;

using Microsoft.EntityFrameworkCore;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLegoThemesFeatures();
builder.Services.AddCatalogDbContext();

builder.Services.AddHealthChecks();

builder.Services.AddOpenApi();

var app = builder.Build();

await app.ApplyDbMigrationsAsync();

app.MapHealthChecks("/health");

app.MapEndpoints();

app.MapOpenApi();
app.MapScalarApiReference();

await app.RunAsync();
