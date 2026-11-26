using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Api.Infrastructure.Validation;
using BrickShare.Catalog.Domain;

using FluentValidation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.Sets.Browse;

public static class BrowseSets
{
    public static void MapBrowseSets(this RouteGroupBuilder sets) =>
        sets.MapGet("/", Handle);

    private static async Task<Results<Ok<PagedResponse<SetSummaryResponse>>, ValidationProblem>> Handle(
        [AsParameters] BrowseSetsRequest request,
        IValidator<BrowseSetsRequest> validator,
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return ValidationProblems.From(validation);
        }

        var matches = dbContext.LegoSets.Where(legoSet => legoSet.IsActive);

        if (request.ThemeId is not null)
        {
            matches = matches.Where(legoSet => legoSet.ThemeId == request.ThemeId);
        }

        if (request.MinPieces is not null)
        {
            matches = matches.Where(legoSet => legoSet.PieceCount >= request.MinPieces);
        }

        if (request.MaxPieces is not null)
        {
            matches = matches.Where(legoSet => legoSet.PieceCount <= request.MaxPieces);
        }

        if (request.MinPrice is not null)
        {
            matches = matches.Where(legoSet => legoSet.DailyPrice.Amount >= request.MinPrice);
        }

        if (request.MaxPrice is not null)
        {
            matches = matches.Where(legoSet => legoSet.DailyPrice.Amount <= request.MaxPrice);
        }

        var totalCount = await matches.CountAsync(cancellationToken);

        var rows = await Sort(matches, request.Sort)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(legoSet => new SetRow(
                legoSet.Id,
                legoSet.LegoSetNumber,
                legoSet.Name,
                legoSet.PieceCount,
                legoSet.DailyPrice.Amount,
                legoSet.DailyPrice.Currency))
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new SetSummaryResponse(
                row.Id, row.LegoSetNumber.Value, row.Name, row.PieceCount, row.DailyPrice, row.Currency))
            .ToList();

        return TypedResults.Ok(new PagedResponse<SetSummaryResponse>(items, request.Page, request.PageSize, totalCount));
    }

    private static IOrderedQueryable<LegoSet> Sort(IQueryable<LegoSet> sets, string sort) => sort switch
    {
        "pieces" => sets.OrderBy(legoSet => legoSet.PieceCount).ThenBy(legoSet => legoSet.Id),
        "price" => sets.OrderBy(legoSet => legoSet.DailyPrice.Amount).ThenBy(legoSet => legoSet.Id),
        _ => sets.OrderBy(legoSet => legoSet.Name).ThenBy(legoSet => legoSet.Id),
    };

    private sealed record SetRow(
        Guid Id, LegoSetNumber LegoSetNumber, string Name, int? PieceCount, long DailyPrice, string Currency);
}