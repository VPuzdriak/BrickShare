using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.SharedKernel.Tests;

public class AggregateRootTests
{
    [Fact]
    public void Confirm_raises_an_order_confirmed_event()
    {
        var id = Guid.CreateVersion7();
        var order = new Order(id);

        order.Confirm();

        order.DomainEvents.Should().ContainSingle().Which.Should().Be(new OrderConfirmed(id));
    }

    [Fact]
    public void ClearDomainEvents_removes_all_events()
    {
        var order = new Order(Guid.CreateVersion7());
        order.Confirm();

        order.ClearDomainEvents();

        order.DomainEvents.Should().BeEmpty();
    }

    private sealed class Order(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Confirm() => Raise(new OrderConfirmed(Id));
    }

    private sealed record OrderConfirmed(Guid OrderId) : IDomainEvent;
}