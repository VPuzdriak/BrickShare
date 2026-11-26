using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.Sets.ImageUpload;

public static class CreateImageUploadUrl
{
    public static void MapCreateImageUploadUrl(this RouteGroupBuilder sets) =>
        sets.MapPost("/{id:guid}/images/upload-url", Handle);

    private static async Task<Results<Ok<ImageUploadUrlResponse>, ProblemHttpResult>> Handle(
        Guid id,
        CatalogDbContext dbContext,
        ISetImageStorage imageStorage,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var setExists = await dbContext.LegoSets.AnyAsync(legoSet => legoSet.Id == id, cancellationToken);

        if (!setExists)
        {
            return SetProblems.NotFound(id);
        }

        var image = new SetImage(Guid.CreateVersion7(), id);
        var expiresAt = timeProvider.GetUtcNow() + SetImage.UploadUrlLifetime;
        var uploadUrl = await imageStorage.CreateUploadUrlAsync(image.OriginalBlobName, expiresAt, cancellationToken);

        dbContext.SetImages.Add(image);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ImageUploadUrlResponse(image.Id, uploadUrl, expiresAt));
    }
}

public sealed record ImageUploadUrlResponse(Guid ImageId, Uri UploadUrl, DateTimeOffset ExpiresAt);