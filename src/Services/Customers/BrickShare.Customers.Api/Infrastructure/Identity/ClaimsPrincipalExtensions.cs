using System;
using System.Security.Claims;

namespace BrickShare.Customers.Api.Infrastructure.Identity;

public static class ClaimsPrincipalExtensions
{
    public static Guid CustomerId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirst("oid")?.Value ?? throw new InvalidOperationException("The token has no oid claim."));
}