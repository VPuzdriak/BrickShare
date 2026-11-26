using System;

using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Domain;

public sealed class LegoSet : AggregateRoot<Guid>
{
    // Used by EF Core to materialize a stored set. EF Core cannot bind the Money parameters to the public constructor.
    private LegoSet()
        : this(Guid.Empty, null!, null!, null!, null!, null!, null!, null!)
    {
    }

    public LegoSet(
        Guid id,
        LegoSetNumber legoSetNumber,
        string name,
        MinRentalDays minRentalDays,
        Money dailyPrice,
        Money deposit,
        Money replacementValue,
        Money lateFeePerDay)
        : base(id)
    {
        LegoSetNumber = legoSetNumber;
        Name = name;
        MinRentalDays = minRentalDays;
        DailyPrice = dailyPrice;
        Deposit = deposit;
        ReplacementValue = replacementValue;
        LateFeePerDay = lateFeePerDay;
        IsActive = true;
    }

    public LegoSetNumber LegoSetNumber { get; private set; }

    public string Name { get; private set; }

    public MinRentalDays MinRentalDays { get; private set; }

    public Money DailyPrice { get; private set; }

    public Money Deposit { get; private set; }

    public Money ReplacementValue { get; private set; }

    public Money LateFeePerDay { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(
        LegoSetNumber legoSetNumber,
        string name,
        MinRentalDays minRentalDays,
        Money dailyPrice,
        Money deposit,
        Money replacementValue,
        Money lateFeePerDay)
    {
        LegoSetNumber = legoSetNumber;
        Name = name;
        MinRentalDays = minRentalDays;
        DailyPrice = dailyPrice;
        Deposit = deposit;
        ReplacementValue = replacementValue;
        LateFeePerDay = lateFeePerDay;
    }

    public void Deactivate() => IsActive = false;
}