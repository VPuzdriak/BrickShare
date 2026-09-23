// src/Catalog/BrickShare.Catalog.Api/Endpoints/RegisterCopiesRequestValidator.cs — new file, replaces RegisterCopyRequestValidator.cs

using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class RegisterCopiesRequestValidator : AbstractValidator<RegisterCopiesRequest>
{
    /// <summary>
    /// A hundred boxes. This is not a claim about delivery vans — it is a bound on one request, so
    /// a client that means to send five cannot accidentally ask this service to mint, hold and
    /// insert a hundred thousand rows in a single transaction that also holds a lock the whole time.
    /// </summary>
    public const int MaximumBatchSize = 100;

    public RegisterCopiesRequestValidator()
    {
        // NotEmpty covers both null and zero-length, which is what makes it the right rule here:
        // { } and { "copies": [] } and { "copies": null } are the same mistake told three ways.
        RuleFor(request => request.Copies).NotEmpty()
            .WithMessage("Register at least one copy. An empty delivery is not a delivery.");

        RuleFor(request => request.Copies)
            .Must(copies => copies is null || copies.Count <= MaximumBatchSize)
            .WithMessage(
                $"Register at most {MaximumBatchSize} copies in one request. "
                + "Split a larger delivery across several.");

        // One rule per box, and the error key carries the index of the box it failed on.
        RuleForEach(request => request.Copies).SetValidator(new CopyToRegisterValidator());
    }
}

public sealed class CopyToRegisterValidator : AbstractValidator<CopyToRegister>
{
    /// <summary>
    /// Fifty kilograms. The heaviest boxed LEGO set weighs about fifteen, so this is not a claim
    /// about LEGO — it is a typo filter. A baseline of 92000 grams instead of 9200 makes every
    /// future return of that copy look catastrophically short.
    /// </summary>
    public const int MaximumBaselineWeightInGrams = 50_000;

    public CopyToRegisterValidator()
    {
        // The backstop for the numeric form. A grade sent as "Sparkly" never reaches this rule —
        // the JSON reader refuses it first — but a grade sent as 99 deserializes happily.
        RuleFor(copy => copy.Grade).IsInEnum()
            .WithMessage("A grade is one of New, Excellent, Good or Fair.");

        RuleFor(copy => copy.BaselineWeightInGrams)
            .InclusiveBetween(1, MaximumBaselineWeightInGrams)
            .WithMessage(
                $"A baseline weight is in grams, between 1 and {MaximumBaselineWeightInGrams}. "
                + "Weigh the box while it is known complete.");
    }
}
