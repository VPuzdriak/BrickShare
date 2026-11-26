using System;
using System.Threading;
using System.Threading.Tasks;

namespace BrickShare.Catalog.Domain;

public interface ISetImageStorage
{
    Task<Uri> CreateUploadUrlAsync(string blobName, DateTimeOffset expiresOn, CancellationToken cancellationToken);
}