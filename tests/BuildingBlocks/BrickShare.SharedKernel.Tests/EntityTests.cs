using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.SharedKernel.Tests;

public class EntityTests
{
    [Fact]
    public void Entities_with_the_same_id_are_equal()
    {
        var id = Guid.CreateVersion7();
        var ticket = new Ticket(id, "Ada");
        var copy = new Ticket(id, "Grace");

        ticket.Should().Be(copy);
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var ticket = new Ticket(Guid.CreateVersion7(), "Ada");
        var other = new Ticket(Guid.CreateVersion7(), "Ada");

        ticket.Should().NotBe(other);
    }

    [Fact]
    public void Entities_of_different_types_with_the_same_id_are_not_equal()
    {
        var id = Guid.CreateVersion7();
        var ticket = new Ticket(id, "Ada");
        var account = new Account(id, "Ada");

        ticket.Should().NotBe(account);
    }

    private sealed class Ticket(Guid id, string title) : Entity<Guid>(id)
    {
        public string Title { get; } = title;
    }

    private sealed class Account(Guid id, string owner) : Entity<Guid>(id)
    {
        public string Owner { get; } = owner;
    }
}