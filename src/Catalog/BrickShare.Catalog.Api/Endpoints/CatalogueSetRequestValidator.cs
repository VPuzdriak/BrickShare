using BrickShare.Catalog.Domain;

using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class CatalogueSetRequestValidator : AbstractValidator<CatalogueSetRequest>
{
    public CatalogueSetRequestValidator()
    {
        // Delegated: the domain owns what a set number is. This asks it. See below.
        RuleFor(request => request.SetNumber)
            .Must(value => SetNumber.TryParse(value, out _))
            .WithMessage($"A set number is required, and cannot be longer than {SetNumber.MaxLength} characters.");

        RuleFor(request => request.Name).NotEmpty()
            .WithMessage("A name is required.");

        RuleFor(request => request.Theme).NotEmpty()
            .WithMessage("A theme is required.");

        // LEGO's first plastic brick shipped in 1949. Anything earlier is a typo.
        RuleFor(request => request.Year).GreaterThanOrEqualTo(1949)
            .WithMessage("A year is required, and LEGO has not existed since before 1949.");

        RuleFor(request => request.PieceCount).GreaterThanOrEqualTo(1)
            .WithMessage("A set has at least one piece.");

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
