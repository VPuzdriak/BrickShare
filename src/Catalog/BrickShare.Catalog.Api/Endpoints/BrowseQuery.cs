using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

/// <summary>
/// What a customer can narrow the catalog by. Every filter is optional, and absent means
/// "do not filter" — never "filter on the default".
/// </summary>
/// <param name="Search">Part of a set's name, or the start of its set number.</param>
/// <param name="ThemeId">An id from GET /catalog/themes.</param>
/// <param name="MinPieces">At least this many pieces.</param>
/// <param name="MaxPieces">At most this many pieces.</param>
/// <param name="Age">The builder's age. A set rated 8+ suits an eight-year-old; one rated 18+ does not.</param>
/// <param name="MaxPrice">Compared against the starting price, so a set with nothing available never matches.</param>
/// <param name="AvailableNow">True keeps only sets with a copy available now. False is not a filter.</param>
/// <param name="Limit">How many sets to return, 1 to 50. 24 when absent.</param>
/// <param name="After">The next value from the previous page, unchanged. Absent for the first page.</param>
public sealed record BrowseQuery(
    string? Search,
    Guid? ThemeId,
    int? MinPieces,
    int? MaxPieces,
    int? Age,
    decimal? MaxPrice,
    bool? AvailableNow,
    int? Limit,
    string? After)
{
    public const int DefaultLimit = 24;
    public const int MaxLimit = 50;
}

public sealed class BrowseQueryValidator : AbstractValidator<BrowseQuery>
{
    public BrowseQueryValidator()
    {
        RuleFor(query => query.MaxPieces)
            .GreaterThanOrEqualTo(query => query.MinPieces)
            .When(query => query.MinPieces is not null)
            .WithMessage("maxPieces is below minPieces, so no set could match.");

        RuleFor(query => query.Age).InclusiveBetween(0, 18)
            .WithMessage("An age is between 0 and 18. Every set rated for adults is 18+.");

        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0m)
            .WithMessage("A price cannot be negative.");

        RuleFor(query => query.Limit).InclusiveBetween(1, BrowseQuery.MaxLimit)
            .WithMessage($"A page holds 1 to {BrowseQuery.MaxLimit} sets.");

        RuleFor(query => query.After)
            .Must(after => BrowseCursor.TryDecode(after, out _))
            .When(query => query.After is not null)
            .WithMessage("after is not a cursor this API issued. Pass next from the previous page unchanged.");
    }
}
