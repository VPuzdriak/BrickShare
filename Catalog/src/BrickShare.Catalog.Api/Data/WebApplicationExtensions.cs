namespace BrickShare.Catalog.Api.Data;

internal static class WebApplicationExtensions {
  public static async ValueTask ApplyDbMigrationsAsync(this WebApplication app) {
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.EnsureCreatedAsync();
  }
}
