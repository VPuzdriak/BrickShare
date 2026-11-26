using System;
using System.Threading;
using System.Threading.Tasks;

using Azure.Storage.Blobs;
using Azure.Storage.Sas;

using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Infrastructure.Storage;

public sealed class BlobSetImageStorage(BlobServiceClient serviceClient, TimeProvider timeProvider) : ISetImageStorage
{
    private const string ContainerName = "set-images";

    public async Task<Uri> CreateUploadUrlAsync(string blobName, DateTimeOffset expiresOn, CancellationToken cancellationToken)
    {
        var startsOn = timeProvider.GetUtcNow();
        var delegationKey = await serviceClient.GetUserDelegationKeyAsync(startsOn, expiresOn, cancellationToken);

        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Create | BlobSasPermissions.Write, expiresOn)
        {
            BlobContainerName = ContainerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = startsOn
        };

        var blobClient = serviceClient.GetBlobContainerClient(ContainerName).GetBlobClient(blobName);

        return blobClient.GenerateUserDelegationSasUri(sasBuilder, delegationKey.Value);
    }
}