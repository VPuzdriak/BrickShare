using System;

namespace BrickShare.Catalog.Domain;

public sealed record MinRentalDays
{
    public MinRentalDays(int days)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);

        Days = days;
    }

    public int Days { get; }
}