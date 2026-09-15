using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Rebrickable;

public sealed record RebrickableSet(
    SetNumber Number,
    string Name,
    int Year,
    int ThemeId,
    int PieceCount,
    Uri? ImageUrl);
