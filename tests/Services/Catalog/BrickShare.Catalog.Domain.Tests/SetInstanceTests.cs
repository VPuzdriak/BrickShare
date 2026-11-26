using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Catalog.Domain.Tests;

public class SetInstanceTests
{
    [Fact]
    public void New_instance_is_available_in_the_given_condition()
    {
        var instance = CreateInstance(InstanceCondition.Good);

        instance.Status.Should().Be(InstanceStatus.Available);
        instance.Condition.Should().Be(InstanceCondition.Good);
    }

    [Fact]
    public void ChangeStatus_sets_the_physical_status()
    {
        var instance = CreateInstance(InstanceCondition.New);

        instance.ChangeStatus(InstanceStatus.InMaintenance);

        instance.Status.Should().Be(InstanceStatus.InMaintenance);
    }

    [Fact]
    public void ChangeCondition_sets_the_condition()
    {
        var instance = CreateInstance(InstanceCondition.New);

        instance.ChangeCondition(InstanceCondition.Worn);

        instance.Condition.Should().Be(InstanceCondition.Worn);
    }

    private static SetInstance CreateInstance(string condition) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        InventoryCode.FromSequence(1),
        condition);
}