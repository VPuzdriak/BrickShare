using System;

using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Features.Sets;

public sealed record SetRequest(
    string LegoSetNumber,
    string Name,
    int MinRentalDays,
    long DailyPrice,
    long Deposit,
    long ReplacementValue,
    long LateFeePerDay,
    string? RebrickableSetNumber = null);

public sealed record SetResponse(Guid Id, string LegoSetNumber, string Name, bool IsActive)
{
    public static SetResponse From(LegoSet legoSet) =>
        new(legoSet.Id, legoSet.LegoSetNumber.Value, legoSet.Name, legoSet.IsActive);
}