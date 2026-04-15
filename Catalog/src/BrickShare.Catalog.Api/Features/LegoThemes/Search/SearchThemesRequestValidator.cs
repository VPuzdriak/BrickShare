using FluentValidation;

namespace BrickShare.Catalog.Api.Features.LegoThemes.Search;

internal sealed class SearchThemesRequestValidator : AbstractValidator<SearchThemesRequest> {
  public SearchThemesRequestValidator() {
    RuleFor(x => x.SearchTerm)
      .NotEmpty()
      .WithMessage("Search term is required.")
      .MinimumLength(3)
      .WithMessage("Search term must be at least 3 characters long.");
  }
}

