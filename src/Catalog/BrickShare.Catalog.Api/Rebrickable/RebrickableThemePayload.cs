using System.Text.Json.Serialization;

namespace BrickShare.Catalog.Api.Rebrickable;

internal sealed record RebrickableThemePayload(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parent_id")]
    int? ParentId);
