using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class RegisterCopyRequestValidator : AbstractValidator<RegisterCopyRequest>
{
    /// <summary>
    /// Fifty kilograms. The heaviest boxed LEGO set weighs about fifteen, so this is not a claim
    /// about LEGO — it is a typo filter. A baseline of 92000 grams instead of 9200 makes every
    /// future return of that copy look catastrophically short.
    /// </summary>
    public const int MaximumBaselineWeightInGrams = 50_000;

    public RegisterCopyRequestValidator()
    {
        // The backstop for the numeric form. A grade sent as "Sparkly" never reaches this rule —
        // the JSON reader refuses it first — but a grade sent as 99 deserializes happily.
        RuleFor(request => request.Grade).IsInEnum()
            .WithMessage("A grade is one of New, Excellent, Good or Fair.");

        RuleFor(request => request.BaselineWeightInGrams)
            .InclusiveBetween(1, MaximumBaselineWeightInGrams)
            .WithMessage(
                $"A baseline weight is in grams, between 1 and {MaximumBaselineWeightInGrams}. "
                + "Weigh the box while it is known complete.");
    }
}
