using System.Text.Json.Serialization;

namespace BrickShare.Catalog.Api.Rebrickable;

internal sealed record RebrickableSetPayload(
    [property: JsonPropertyName("set_num")]
    string SetNumber,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("theme_id")]
    int ThemeId,
    [property: JsonPropertyName("num_parts")]
    int PieceCount,
    [property: JsonPropertyName("set_img_url")]
    Uri? ImageUrl);
