using System.Reflection;

using BrickShare.Rent.Api.Data;
using BrickShare.Rent.Api.Endpoints;
using BrickShare.Rent.Api.Features.LegoSetInstances;

using FluentValidation;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);
builder.Services.AddLegoSetFeatures();
builder.Services.AddRentDbContext();

builder.Services.AddHealthChecks();

builder.Services.AddOpenApi();

var app = builder.Build();

await app.ApplyDbMigrationsAsync();

app.MapHealthChecks("/health");

app.MapEndpoints();

app.MapOpenApi();
app.MapScalarApiReference();

await app.RunAsync();
