using System;

namespace BrickShare.Catalog.Domain;

public sealed record SetSpecification(
    RebrickableSetNumber RebrickableSetNumber,
    string Name,
    int? Year,
    int PieceCount,
    int MinifigureCount,
    RebrickableTheme Theme,
    Uri? ImageUrl,
    Uri PageUrl);