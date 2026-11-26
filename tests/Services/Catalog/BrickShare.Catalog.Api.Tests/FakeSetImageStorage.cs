using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Tests;

public sealed class FakeSetImageStorage : ISetImageStorage
{
    public string? LastBlobName { get; private set; }

    public Task<Uri> CreateUploadUrlAsync(string blobName, DateTimeOffset expiresOn, CancellationToken cancellationToken)
    {
        LastBlobName = blobName;

        return Task.FromResult(new Uri($"https://storage.test/set-images/{blobName}?sig=test"));
    }
}