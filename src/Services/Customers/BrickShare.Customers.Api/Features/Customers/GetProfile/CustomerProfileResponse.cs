using System;

namespace BrickShare.Customers.Api.Features.Customers.GetProfile;

public sealed record CustomerProfileResponse(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);