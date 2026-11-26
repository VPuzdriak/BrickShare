namespace BrickShare.Catalog.Domain;

public sealed record RebrickableTheme(int Id, string Name, RebrickableTheme? Parent)
{
    public RebrickableTheme TopLevel => Parent is null ? this : Parent.TopLevel;
}