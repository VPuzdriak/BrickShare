using FluentValidation;

namespace BrickShare.Catalog.Api.Features.LegoSets.Search;

internal sealed class SearchLegoSetsRequestValidator : AbstractValidator<SearchLegoSetsRequest> {
  public SearchLegoSetsRequestValidator() {
    RuleFor(x => x.SearchTerm)
      .MinimumLength(3)
      .WithMessage("Search term must be at least 3 characters long.")
      .When(x => x.SearchTerm is not null);

    RuleFor(x => x)
      .Must(x => x.SearchTerm is not null || x.ThemeId is not null)
      .WithMessage("Either search term or theme id must be provided.");

    RuleFor(x => x.Page)
      .GreaterThanOrEqualTo(1)
      .WithMessage("Page must be at least 1.");

    RuleFor(x => x.PageSize)
      .InclusiveBetween(1, 100)
      .WithMessage("Page size must be between 1 and 100.");
  }
}

