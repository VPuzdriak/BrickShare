using BrickShare.Rent.Api.Features.LegoSetInstances.Add;

namespace BrickShare.Rent.Api.Features.LegoSetInstances;

internal static class ServiceCollectionExtensions {
  public static IServiceCollection AddLegoSetFeatures(this IServiceCollection services) {
    services.AddScoped<AddLegoInstanceHandler>();
    return services;
  }
}
