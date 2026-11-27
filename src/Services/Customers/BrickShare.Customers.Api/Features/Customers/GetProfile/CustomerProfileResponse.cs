using System;

using BrickShare.Customers.Domain;

namespace BrickShare.Customers.Api.Features.Customers.GetProfile;

public sealed record CustomerProfileResponse(Guid Id, string Email, string DisplayName, string? Phone, DateTimeOffset CreatedAt)
{
    public static CustomerProfileResponse From(Customer customer) =>
        new(customer.Id, customer.Email, customer.DisplayName, customer.Phone, customer.CreatedAt);
}