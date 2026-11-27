using System;

using BrickShare.Customers.Domain;

namespace BrickShare.Customers.Api.Features.Customers.BanCustomer;

public sealed record BanCustomerRequest(BanReason Reason, string? Comment, DateTimeOffset? Until);