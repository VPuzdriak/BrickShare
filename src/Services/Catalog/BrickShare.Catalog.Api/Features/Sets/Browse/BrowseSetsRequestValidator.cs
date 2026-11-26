using FluentValidation;

namespace BrickShare.Catalog.Api.Features.Sets.Browse;

public sealed class BrowseSetsRequestValidator : AbstractValidator<BrowseSetsRequest>
{
    public BrowseSetsRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(request => request.Sort)
            .Must(sort => sort is "name" or "pieces" or "price")
            .WithMessage("Sort must be name, pieces, or price.");
    }
}