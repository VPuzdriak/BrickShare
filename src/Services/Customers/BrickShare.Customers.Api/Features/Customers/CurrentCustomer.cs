using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Customers.Api.Infrastructure.Identity;
using BrickShare.Customers.Api.Infrastructure.Persistence;
using BrickShare.Customers.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Customers.Api.Features.Customers;

public static class CurrentCustomer
{
    public static async Task<Customer> LoadOrCreateAsync(
        ClaimsPrincipal user,
        CustomerDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var customerId = user.CustomerId();

        var customer = await dbContext.Customers.FirstOrDefaultAsync(
            candidate => candidate.Id == customerId, cancellationToken);

        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer(
            customerId,
            user.FindFirst("email")?.Value ?? string.Empty,
            user.FindFirst("name")?.Value ?? string.Empty,
            timeProvider.GetUtcNow());

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return customer;
    }
}