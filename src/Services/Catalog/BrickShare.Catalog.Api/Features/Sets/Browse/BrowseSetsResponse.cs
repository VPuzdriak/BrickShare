using System;
using System.Collections.Generic;

namespace BrickShare.Catalog.Api.Features.Sets.Browse;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record SetSummaryResponse(
    Guid Id,
    string LegoSetNumber,
    string Name,
    int? PieceCount,
    long DailyPrice,
    string Currency);