using Microsoft.EntityFrameworkCore;

namespace BrickShare.Rent.Api.Data;

internal static class ServiceCollectionExtensions {
  public static void AddRentDbContext(this IServiceCollection services) {
    services.AddDbContext<RentalDbContext>(options => options.UseInMemoryDatabase("RentDb"));
  }
}
