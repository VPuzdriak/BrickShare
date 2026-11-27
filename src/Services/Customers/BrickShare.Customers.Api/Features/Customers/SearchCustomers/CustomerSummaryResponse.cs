using System;

namespace BrickShare.Customers.Api.Features.Customers.SearchCustomers;

public sealed record CustomerSummaryResponse(Guid Id, string Email, string DisplayName);