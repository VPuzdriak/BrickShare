using FluentValidation;

namespace BrickShare.Catalog.Api.Features.LegoThemes.Add;

internal sealed class AddLegoThemeRequestValidator : AbstractValidator<AddLegoThemeRequest> {
  public AddLegoThemeRequestValidator() {
    RuleFor(x => x.Name)
      .NotEmpty()
      .WithMessage("Name is required.");
  }
}

