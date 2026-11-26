using System;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Domain;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class SetImagePersistenceTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Saved_image_is_read_back_as_uploaded_with_its_original_blob_name()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        var image = await SetTestData.SaveImageAsync(Factory, new SetImage(Guid.CreateVersion7(), legoSet.Id));

        var stored = await SetTestData.FindImageAsync(Factory, image.Id);

        stored!.SetId.Should().Be(legoSet.Id);
        stored.Status.Should().Be(SetImageStatus.Uploaded);
        stored.OriginalBlobName.Should().Be($"originals/{legoSet.Id}/{image.Id}");
    }
}