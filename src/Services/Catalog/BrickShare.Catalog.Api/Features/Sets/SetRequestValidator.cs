using FluentValidation;

namespace BrickShare.Catalog.Api.Features.Sets;

public sealed class SetRequestValidator : AbstractValidator<SetRequest>
{
    public SetRequestValidator()
    {
        RuleFor(request => request.LegoSetNumber)
            .NotEmpty()
            .Matches(@"\A[0-9]+\z");

        RuleFor(request => request.Name)
            .NotEmpty();

        RuleFor(request => request.MinRentalDays)
            .GreaterThanOrEqualTo(1);

        RuleFor(request => request.DailyPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(request => request.Deposit)
            .GreaterThanOrEqualTo(0);

        RuleFor(request => request.ReplacementValue)
            .GreaterThanOrEqualTo(0);

        RuleFor(request => request.LateFeePerDay)
            .GreaterThanOrEqualTo(0);
    }
}