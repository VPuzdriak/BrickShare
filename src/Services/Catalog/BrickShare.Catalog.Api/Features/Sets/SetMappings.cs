using System;

using BrickShare.Catalog.Domain;
using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Api.Features.Sets;

public static class SetMappings
{
    private const string Currency = "EUR";

    public static LegoSet ToLegoSet(this SetRequest request, Guid id) => new(
        id,
        new LegoSetNumber(request.LegoSetNumber),
        request.Name,
        new MinRentalDays(request.MinRentalDays),
        Price(request.DailyPrice),
        Price(request.Deposit),
        Price(request.ReplacementValue),
        Price(request.LateFeePerDay));

    public static void ApplyTo(this SetRequest request, LegoSet legoSet) => legoSet.Update(
        new LegoSetNumber(request.LegoSetNumber),
        request.Name,
        new MinRentalDays(request.MinRentalDays),
        Price(request.DailyPrice),
        Price(request.Deposit),
        Price(request.ReplacementValue),
        Price(request.LateFeePerDay));

    private static Money Price(long amount) => new(amount, Currency);
}