using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Customers.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Customers.Api.Features.Customers.SearchCustomers;

public static class SearchCustomers
{
    private const int MaxResults = 50;
    private const string EscapeCharacter = "\\";

    public static void MapSearchCustomers(this RouteGroupBuilder customers) =>
        customers.MapGet("/", Handle);

    private static async Task<Results<Ok<List<CustomerSummaryResponse>>, ProblemHttpResult>> Handle(
        string? search,
        CustomerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var term = search?.Trim();

        if (string.IsNullOrEmpty(term))
        {
            return CustomerProblems.SearchTextRequired();
        }

        var pattern = $"%{EscapeLike(term)}%";

        var customers = await dbContext.Customers
            .Where(customer => EF.Functions.ILike(customer.DisplayName, pattern, EscapeCharacter)
                || EF.Functions.ILike(customer.Email, pattern, EscapeCharacter))
            .OrderBy(customer => customer.DisplayName)
            .ThenBy(customer => customer.Email)
            .Take(MaxResults)
            .Select(customer => new CustomerSummaryResponse(customer.Id, customer.Email, customer.DisplayName))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(customers);
    }

    private static string EscapeLike(string term) =>
        term.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}