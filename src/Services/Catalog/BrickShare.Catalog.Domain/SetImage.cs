using System;

using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Domain;

public sealed class SetImage : AggregateRoot<Guid>
{
    public static readonly TimeSpan UploadUrlLifetime = TimeSpan.FromMinutes(15);

    // Used by EF Core to materialize a stored image.
    private SetImage()
        : this(Guid.Empty, Guid.Empty)
    {
    }

    public SetImage(Guid id, Guid setId)
        : base(id)
    {
        SetId = setId;
        OriginalBlobName = $"originals/{setId}/{id}";
        Status = SetImageStatus.Uploaded;
    }

    public Guid SetId { get; private set; }

    public string OriginalBlobName { get; private set; }

    public string Status { get; private set; }
}