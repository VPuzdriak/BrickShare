using System;

using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Domain;

public sealed class LegoSet(
    Guid id,
    LegoSetNumber legoSetNumber,
    MinRentalDays minRentalDays,
    Money dailyPrice,
    Money deposit,
    Money replacementValue,
    Money lateFeePerDay) : AggregateRoot<Guid>(id)
{
    public LegoSetNumber LegoSetNumber { get; } = legoSetNumber;

    public MinRentalDays MinRentalDays { get; } = minRentalDays;

    public Money DailyPrice { get; } = dailyPrice;

    public Money Deposit { get; } = deposit;

    public Money ReplacementValue { get; } = replacementValue;

    public Money LateFeePerDay { get; } = lateFeePerDay;
}