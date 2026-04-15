using BrickShare.Catalog.Api.Features.LegoSets.Add;
using BrickShare.Catalog.Api.Features.LegoSets.Retrieve;
using BrickShare.Catalog.Api.Features.LegoSets.Search;

using FluentValidation;

namespace BrickShare.Catalog.Api.Features.LegoSets;

internal static class ServiceCollectionExtensions {
  public static void AddLegoSetsFeatures(this IServiceCollection services) {
    services.AddScoped<AddLegoSetHandler>();
    services.AddScoped<GetLegoSetHandler>();
    services.AddScoped<SearchLegoSetsHandler>();

    services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly, includeInternalTypes: true);
  }
}

