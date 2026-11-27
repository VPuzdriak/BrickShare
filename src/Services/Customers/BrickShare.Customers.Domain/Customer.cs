using System;

using BrickShare.SharedKernel;

namespace BrickShare.Customers.Domain;

public sealed class Customer : AggregateRoot<Guid>
{
    private Customer()
        : this(Guid.Empty, string.Empty, string.Empty, default)
    {
    }

    public Customer(Guid id, string email, string displayName, DateTimeOffset createdAt)
        : base(id)
    {
        Email = email;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}