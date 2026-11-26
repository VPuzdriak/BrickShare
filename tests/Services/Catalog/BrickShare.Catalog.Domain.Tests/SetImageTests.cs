using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class SetImageTests
{
    [Fact]
    public void New_image_is_uploaded_to_its_original_blob_under_the_set()
    {
        var setId = Guid.CreateVersion7();
        var imageId = Guid.CreateVersion7();

        var image = new SetImage(imageId, setId);

        image.SetId.Should().Be(setId);
        image.Status.Should().Be(SetImageStatus.Uploaded);
        image.OriginalBlobName.Should().Be($"originals/{setId}/{imageId}");
    }
}