using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Api.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.Sets.Details;

public static class GetSetDetails
{
    public static void MapGetSetDetails(this RouteGroupBuilder sets) =>
        sets.MapGet("/{id:guid}", Handle);

    private static async Task<Results<Ok<SetDetailsResponse>, ProblemHttpResult>> Handle(
        Guid id, CatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        var legoSet = await dbContext.LegoSets.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.IsActive, cancellationToken);

        if (legoSet is null)
        {
            return SetProblems.NotFound(id);
        }

        var themeName = await dbContext.Themes
            .Where(theme => theme.Id == legoSet.ThemeId)
            .Select(theme => theme.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return TypedResults.Ok(new SetDetailsResponse(
            legoSet.Id,
            legoSet.LegoSetNumber.Value,
            legoSet.Name,
            legoSet.Year,
            legoSet.PieceCount,
            legoSet.MinifigureCount,
            themeName,
            legoSet.MinRentalDays.Days,
            legoSet.DailyPrice.Amount,
            legoSet.Deposit.Amount,
            legoSet.ReplacementValue.Amount,
            legoSet.LateFeePerDay.Amount,
            legoSet.DailyPrice.Currency));
    }
}

public sealed record SetDetailsResponse(
    Guid Id,
    string LegoSetNumber,
    string Name,
    int? Year,
    int? PieceCount,
    int? MinifigureCount,
    string? ThemeName,
    int MinRentalDays,
    long DailyPrice,
    long Deposit,
    long ReplacementValue,
    long LateFeePerDay,
    string Currency);