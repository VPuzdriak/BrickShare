using FluentValidation;

namespace BrickShare.Catalog.Api.Features.Instances.LabelSheet;

public sealed class LabelSheetRequestValidator : AbstractValidator<LabelSheetRequest>
{
    public LabelSheetRequestValidator()
    {
        RuleFor(request => request.InstanceIds)
            .NotEmpty();
    }
}