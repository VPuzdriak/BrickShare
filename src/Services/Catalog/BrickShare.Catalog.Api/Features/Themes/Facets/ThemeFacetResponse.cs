using System;

namespace BrickShare.Catalog.Api.Features.Themes.Facets;

public sealed record ThemeFacetResponse(Guid Id, string Name, int SetCount);