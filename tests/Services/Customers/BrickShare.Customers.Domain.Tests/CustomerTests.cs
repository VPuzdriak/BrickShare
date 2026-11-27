using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.Customers.Domain.Tests;

public class CustomerTests
{
    [Fact]
    public void Registered_customer_keeps_the_id_profile_and_creation_time()
    {
        var id = Guid.CreateVersion7();
        var createdAt = new DateTimeOffset(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

        var customer = new Customer(id, "ada@example.test", "Ada Lovelace", createdAt);

        customer.Id.Should().Be(id);
        customer.Email.Should().Be("ada@example.test");
        customer.DisplayName.Should().Be("Ada Lovelace");
        customer.CreatedAt.Should().Be(createdAt);
    }
}