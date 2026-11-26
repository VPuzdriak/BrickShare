using System;

using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Domain;

public sealed class Theme : AggregateRoot<Guid>
{
    private Theme(Guid id, string name, int rebrickableThemeId)
        : base(id)
    {
        Name = name;
        RebrickableThemeId = rebrickableThemeId;
    }

    public string Name { get; }

    public int RebrickableThemeId { get; }

    public static Theme FromRebrickable(Guid id, RebrickableTheme topLevelTheme)
    {
        if (topLevelTheme.Parent is not null)
        {
            throw new ArgumentException("Only a top-level Rebrickable theme becomes a catalog theme.", nameof(topLevelTheme));
        }

        return new Theme(id, topLevelTheme.Name, topLevelTheme.Id);
    }
}