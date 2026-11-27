using System;

using BrickShare.SharedKernel;

namespace BrickShare.Customers.Domain;

public sealed class Customer : AggregateRoot<Guid>
{
    private const int MaxDisplayNameLength = 100;
    private const int MaxPhoneLength = 32;

    private static readonly DomainError DisplayNameRequired = new(
        "customer.display_name_required",
        "A display name is required.");

    private static readonly DomainError DisplayNameTooLong = new(
        "customer.display_name_too_long",
        "A display name has at most 100 characters.");

    private static readonly DomainError PhoneTooLong = new(
        "customer.phone_too_long",
        "A phone number has at most 32 characters.");

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

    public string? Phone { get; private set; }

    public Result UpdateProfile(string? displayName, string? phone)
    {
        var name = displayName?.Trim() ?? string.Empty;
        var number = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

        if (name.Length == 0)
        {
            return Result.Failure(DisplayNameRequired);
        }

        if (name.Length > MaxDisplayNameLength)
        {
            return Result.Failure(DisplayNameTooLong);
        }

        if (number?.Length > MaxPhoneLength)
        {
            return Result.Failure(PhoneTooLong);
        }

        DisplayName = name;
        Phone = number;

        return Result.Success();
    }
}