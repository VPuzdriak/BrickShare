using System.ComponentModel.DataAnnotations;

namespace BrickShare.Catalog.Api.Rebrickable;

public sealed class RebrickableOptions
{
    public const string SectionName = "Rebrickable";

    [Required] public Uri BaseAddress { get; set; } = new("https://rebrickable.com/api/v3/");

    [Required] public string ApiKey { get; set; } = string.Empty;
}
