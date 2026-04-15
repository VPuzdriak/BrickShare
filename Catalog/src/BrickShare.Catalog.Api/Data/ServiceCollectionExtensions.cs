using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Data;

internal static class ServiceCollectionExtensions {
  public static void AddCatalogDbContext(this IServiceCollection services) {
    services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase("CatalogDb"));
  }
}
