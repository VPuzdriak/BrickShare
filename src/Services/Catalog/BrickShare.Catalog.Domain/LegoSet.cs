using System;

using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Domain;

public sealed class LegoSet : AggregateRoot<Guid>
{
    // Used by EF Core to materialize a stored set. EF Core cannot bind the Money parameters to the public constructor.
    private LegoSet()
        : this(Guid.Empty, null!, null!, null!, null!, null!, null!)
    {
    }

    public LegoSet(
        Guid id,
        LegoSetNumber legoSetNumber,
        MinRentalDays minRentalDays,
        Money dailyPrice,
        Money deposit,
        Money replacementValue,
        Money lateFeePerDay)
        : base(id)
    {
        LegoSetNumber = legoSetNumber;
        MinRentalDays = minRentalDays;
        DailyPrice = dailyPrice;
        Deposit = deposit;
        ReplacementValue = replacementValue;
        LateFeePerDay = lateFeePerDay;
    }

    public LegoSetNumber LegoSetNumber { get; }

    public MinRentalDays MinRentalDays { get; }

    public Money DailyPrice { get; }

    public Money Deposit { get; }

    public Money ReplacementValue { get; }

    public Money LateFeePerDay { get; }
}