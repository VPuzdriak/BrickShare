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

    [Fact]
    public void Profile_update_changes_the_display_name_and_phone()
    {
        var customer = CreateCustomer();

        var result = customer.UpdateProfile("Ada King", "+44 20 7946 0000");

        result.IsSuccess.Should().BeTrue();
        customer.DisplayName.Should().Be("Ada King");
        customer.Phone.Should().Be("+44 20 7946 0000");
    }

    [Fact]
    public void Profile_update_fails_when_the_display_name_is_blank()
    {
        var customer = CreateCustomer();

        var result = customer.UpdateProfile("   ", null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("customer.display_name_required");
        customer.DisplayName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public void Profile_update_fails_when_the_display_name_is_longer_than_100_characters()
    {
        var customer = CreateCustomer();

        var result = customer.UpdateProfile(new string('a', 101), null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("customer.display_name_too_long");
    }

    [Fact]
    public void Profile_update_fails_when_the_phone_is_longer_than_32_characters()
    {
        var customer = CreateCustomer();

        var result = customer.UpdateProfile("Ada King", new string('1', 33));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("customer.phone_too_long");
    }

    [Fact]
    public void Profile_update_clears_a_blank_phone()
    {
        var customer = CreateCustomer();

        var result = customer.UpdateProfile("Ada King", "   ");

        result.IsSuccess.Should().BeTrue();
        customer.Phone.Should().BeNull();
    }

    private static Customer CreateCustomer() =>
        new(Guid.CreateVersion7(), "ada@example.test", "Ada Lovelace", new DateTimeOffset(2026, 11, 27, 9, 0, 0, TimeSpan.Zero));
}