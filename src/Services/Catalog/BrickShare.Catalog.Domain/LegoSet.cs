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

    public string? RebrickableSetNum { get; private set; }

    public int? Year { get; private set; }

    public int? PieceCount { get; private set; }

    public int? MinifigureCount { get; private set; }

    public Guid? ThemeId { get; private set; }

    public DateTimeOffset? SpecsFetchedAt { get; private set; }

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

    public void LinkToRebrickable(SetSpecification specification, Guid themeId, DateTimeOffset fetchedAt)
    {
        RebrickableSetNum = specification.RebrickableSetNumber.Value;
        ApplyRebrickableFacts(specification, themeId, fetchedAt);
    }

    public void RefreshSpecification(SetSpecification specification, Guid themeId, DateTimeOffset fetchedAt)
    {
        Name = specification.Name;
        ApplyRebrickableFacts(specification, themeId, fetchedAt);
    }

    private void ApplyRebrickableFacts(SetSpecification specification, Guid themeId, DateTimeOffset fetchedAt)
    {
        Year = specification.Year;
        PieceCount = specification.PieceCount;
        MinifigureCount = specification.MinifigureCount;
        ThemeId = themeId;
        SpecsFetchedAt = fetchedAt;
    }
}