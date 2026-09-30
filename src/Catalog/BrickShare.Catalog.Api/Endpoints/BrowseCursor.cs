using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace BrickShare.Catalog.Api.Endpoints;

/// <summary>
/// Where a page of the catalog ended, in the order the catalog is listed: by name, then by id.
/// Clients receive it as an opaque string and hand it back unchanged. Opaque is not secret: anyone
/// can decode it, and it holds nothing that was not already on the page.
/// </summary>
public sealed record BrowseCursor(string Name, Guid Id)
{
    // Base64Url, not Base64: '+', '/' and '=' all mean something in a query string.
    public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

    public static bool TryDecode(string? value, [NotNullWhen(true)] out BrowseCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            cursor = JsonSerializer.Deserialize<BrowseCursor>(Base64Url.DecodeFromChars(value));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }

        // Valid JSON that is not a cursor, such as {}, decodes with no name.
        if (cursor is not { Name: not null })
        {
            cursor = null;
            return false;
        }

        return true;
    }
}
