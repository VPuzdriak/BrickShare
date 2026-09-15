using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

namespace BrickShare.Catalog.Api.Rebrickable;

public static class RebrickableServiceCollectionExtensions
{
    public static IServiceCollection AddRebrickable(
        this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(RebrickableOptions.SectionName);

        services.AddOptions<RebrickableOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IRebrickableCatalog, RebrickableClient>((sp, client) =>
            {
                RebrickableOptions options =
                    sp.GetRequiredService<IOptions<RebrickableOptions>>().Value;

                client.BaseAddress = options.BaseAddress;

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("key", options.ApiKey);
            })
            .AddStandardResilienceHandler(section.GetSection("Resilience"));


        return services;
    }
}
