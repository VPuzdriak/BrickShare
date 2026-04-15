using BrickShare.Catalog.Api.Data;
using BrickShare.Catalog.Api.Endpoints;
using BrickShare.Catalog.Api.Features.LegoThemes;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLegoThemesFeatures();
builder.Services.AddCatalogDbContext();

builder.Services.AddHealthChecks();

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapEndpoints();

app.MapOpenApi();
app.MapScalarApiReference();

app.Run();
