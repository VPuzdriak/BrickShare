// src/Catalog/BrickShare.Catalog.Api/Endpoints/LookupRequestValidator.cs — new file
using BrickShare.Catalog.Domain;

using FluentValidation;

namespace BrickShare.Catalog.Api.Endpoints;

public sealed class LookupRequestValidator : AbstractValidator<LookupRequest>
{
    public LookupRequestValidator()
    {
        // Delegated, exactly as episode 22 delegated it: the domain owns what a set number is.
        // This rule also keeps a malformed number from costing a Rebrickable request.
        RuleFor(request => request.SetNumber)
            .Must(value => SetNumber.TryParse(value, out _))
            .WithMessage($"A set number is required, and cannot be longer than {SetNumber.MaxLength} characters.");
    }
}
