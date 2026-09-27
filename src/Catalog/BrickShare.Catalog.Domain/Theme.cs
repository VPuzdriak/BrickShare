namespace BrickShare.Catalog.Domain;

/// <summary>
/// A theme the shop can group sets under and customers can filter on. Adopted from Rebrickable
/// rather than invented here: <see cref="RebrickableId"/> is what makes two catalogued sets agree
/// that they are the same theme, even if the name upstream changes afterwards.
/// </summary>
public sealed class Theme
{
    public const int MaxNameLength = 100;

    private Theme(int rebrickableId, string name)
    {
        Id = Guid.CreateVersion7();
        RebrickableId = rebrickableId;
        Name = name;
    }

    public Guid Id { get; }

    public int RebrickableId { get; }

    public string Name { get; }

    public static Theme Adopt(int rebrickableId, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rebrickableId, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Theme(rebrickableId, name);
    }
}
