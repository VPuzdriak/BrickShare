using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class CatalogueSetRequestValidator : AbstractValidator<CatalogueSetRequest>
{
    public CatalogueSetRequestValidator()
    {
        RuleFor(request => request.LookupId).NotEmpty()
            .WithMessage("A lookupId is required. Look the set up first: POST /api/v1/catalog/lookups.");

        RuleFor(request => request.RetailPrice).GreaterThanOrEqualTo(0m)
            .WithMessage("A retail price cannot be negative.");

        RuleFor(request => request.BaseRentalPrice).GreaterThanOrEqualTo(0m)
            .WithMessage("A base rental price cannot be negative.");

        RuleFor(request => request.MinimumRentalDays).GreaterThanOrEqualTo(1)
            .WithMessage("A rental lasts at least one day.");

        RuleFor(request => request.MinimumAge).InclusiveBetween(0, 18)
            .WithMessage("An age rating is between 0 and 18.");
    }
}
