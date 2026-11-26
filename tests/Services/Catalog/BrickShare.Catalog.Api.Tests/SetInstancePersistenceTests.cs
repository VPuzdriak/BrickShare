using System;
using System.Threading.Tasks;

using AwesomeAssertions;

using BrickShare.Catalog.Domain;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[Collection(CatalogCollectionDefinition.Name)]
public class SetInstancePersistenceTests(CatalogApiFactory factory) : CatalogDatabaseTests(factory)
{
    [Fact]
    public async Task Saved_instance_is_read_back_with_its_code_condition_and_status()
    {
        var legoSet = await SetTestData.SaveAsync(Factory, SetTestData.CreateSet());
        var instance = new SetInstance(
            Guid.CreateVersion7(),
            legoSet.Id,
            InventoryCode.FromSequence(1),
            InstanceCondition.Good);
        instance.ChangeStatus(InstanceStatus.Retired);
        await SetTestData.SaveInstanceAsync(Factory, instance);

        var stored = await SetTestData.FindInstanceAsync(Factory, instance.Id);

        stored!.SetId.Should().Be(legoSet.Id);
        stored.InventoryCode.Value.Should().Be(instance.InventoryCode.Value);
        stored.Condition.Should().Be(InstanceCondition.Good);
        stored.Status.Should().Be(InstanceStatus.Retired);
    }
}