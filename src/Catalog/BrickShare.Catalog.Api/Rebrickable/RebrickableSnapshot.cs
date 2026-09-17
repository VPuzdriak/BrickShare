// src/Catalog/BrickShare.Catalog.Api/Rebrickable/RebrickableSnapshot.cs — new file

using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Rebrickable;

public sealed class RebrickableSnapshot
{
    public Guid Id { get; private set; }

    public SetNumber Number { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public int ThemeId { get; private set; }

    public string ThemeName { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public int PieceCount { get; private set; }

    public Uri? ImageUrl { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    private RebrickableSnapshot()
    {
    }

    public static RebrickableSnapshot Capture(RebrickableSet set, RebrickableTheme theme, DateTimeOffset fetchedAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Number = set.Number,
            Name = set.Name,
            ThemeId = theme.Id,
            ThemeName = theme.Name,
            Year = set.Year,
            PieceCount = set.PieceCount,
            ImageUrl = set.ImageUrl,
            FetchedAt = fetchedAt
        };
}
