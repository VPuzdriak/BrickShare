using System;

using Azure.Identity;
using Azure.Storage.Blobs;

using BrickShare.Catalog.Domain;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Catalog.Api.Infrastructure.Storage;

public static class StorageRegistration
{
    public static IServiceCollection AddSetImageStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceUri = configuration["Storage:BlobServiceUri"]
            ?? throw new InvalidOperationException("The setting 'Storage:BlobServiceUri' is missing.");

        services.AddSingleton(_ => new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential()));
        services.AddSingleton<ISetImageStorage, BlobSetImageStorage>();

        return services;
    }
}