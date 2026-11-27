using System;
using System.Threading.Tasks;

using BrickShare.Customers.Api.Infrastructure.Persistence;
using BrickShare.Customers.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Customers.Api.Tests;

public static class CustomerTestData
{
    public static async Task<Guid> SaveCustomerAsync(CustomerApiFactory factory)
    {
        var customer = new Customer(Guid.CreateVersion7(), "ada@example.test", "Ada Lovelace", factory.Time.GetUtcNow());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        return customer.Id;
    }

    public static async Task<Guid> BanAsync(CustomerApiFactory factory, Guid customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        var customer = await dbContext.Customers.SingleAsync(candidate => candidate.Id == customerId);
        var ban = customer.AddBan(BanReason.LateReturn, "Returned late.", "staff-1", null, factory.Time.GetUtcNow()).Value;
        await dbContext.SaveChangesAsync();

        return ban.Id;
    }

    public static async Task<Customer> FindAsync(CustomerApiFactory factory, Guid customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();

        return await dbContext.Customers.SingleAsync(candidate => candidate.Id == customerId);
    }
}