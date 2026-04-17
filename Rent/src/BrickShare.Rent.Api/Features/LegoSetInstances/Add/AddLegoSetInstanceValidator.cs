using FluentValidation;

namespace BrickShare.Rent.Api.Features.LegoSetInstances.Add;

internal sealed class AddLegoSetInstanceRequestValidator : AbstractValidator<AddLegoSetInstanceRequest> {
  public AddLegoSetInstanceRequestValidator() {
    RuleFor(request => request.SetId)
      .NotEmpty()
      .WithMessage("Set ID is required.");

    RuleFor(request => request.PricePerDay)
      .NotEmpty()
      .GreaterThanOrEqualTo(5)
      .WithMessage("Price per day is required.");

    RuleFor(request => request.MinimalRentalDays)
      .GreaterThanOrEqualTo(5)
      .WithMessage("Minimal rent days is required.");

    RuleFor(request => request.ConditionScore)
      .InclusiveBetween(80, 100)
      .WithMessage("Condition score is required.");
  }
}
