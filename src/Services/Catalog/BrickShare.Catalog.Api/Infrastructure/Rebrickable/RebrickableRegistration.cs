using System;
using System.Net.Http.Headers;

using BrickShare.Catalog.Domain;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Catalog.Api.Infrastructure.Rebrickable;

public static class RebrickableRegistration
{
    public static IServiceCollection AddRebrickableLookup(this IServiceCollection services, IConfiguration configuration)
    {
        var rebrickable = configuration.GetSection("Rebrickable");

        var baseUrl = rebrickable["BaseUrl"]
            ?? throw new InvalidOperationException("The setting 'Rebrickable:BaseUrl' is missing.");

        var apiKey = rebrickable["ApiKey"]
            ?? throw new InvalidOperationException("The setting 'Rebrickable:ApiKey' is missing. Set it with dotnet user-secrets.");

        services.AddHttpClient<ISetSpecificationProvider, RebrickableSetSpecificationProvider>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("key", apiKey);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 2;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            });

        return services;
    }
}