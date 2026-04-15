using FluentValidation;

namespace BrickShare.Catalog.Api.Features.LegoSets.Add;

internal sealed class AddLegoSetRequestValidator : AbstractValidator<AddLegoSetRequest> {
  public AddLegoSetRequestValidator() {
    RuleFor(x => x.Name)
      .NotEmpty()
      .WithMessage("Name is required.");

    RuleFor(x => x.LegoId)
      .NotEmpty()
      .WithMessage("Lego ID is required.");

    RuleFor(x => x.NumberOfParts)
      .GreaterThan(0)
      .WithMessage("Number of parts must be greater than 0.");

    RuleFor(x => x.AgeFrom)
      .InclusiveBetween(3, 18)
      .WithMessage("Age from must be between 3 and 18.");

    RuleFor(x => x.ThemeId)
      .NotEmpty()
      .WithMessage("Theme ID is required.");
  }
}
