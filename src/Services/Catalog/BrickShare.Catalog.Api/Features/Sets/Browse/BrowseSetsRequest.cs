using System;

namespace BrickShare.Catalog.Api.Features.Sets.Browse;

public sealed record BrowseSetsRequest(
    Guid? ThemeId,
    int? MinPieces,
    int? MaxPieces,
    long? MinPrice,
    long? MaxPrice,
    string Sort = "name",
    int Page = 1,
    int PageSize = 20);