var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => new { service = "BrickShare Catalog API" });

app.Run();
