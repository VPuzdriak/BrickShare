using BrickShare.Catalog.Api.Features.LegoThemes.Add;
using BrickShare.Catalog.Api.Features.LegoThemes.Retrieve;
using BrickShare.Catalog.Api.Features.LegoThemes.Search;

using FluentValidation;

namespace BrickShare.Catalog.Api.Features.LegoThemes;

internal static class ServiceCollectionExtensions {
  public static void AddLegoThemesFeatures(this IServiceCollection services) {
    services.AddScoped<GetThemesHandler>();
    services.AddScoped<AddThemeHandler>();
    services.AddScoped<SearchThemesHandler>();

    services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly, includeInternalTypes: true);
  }
}
