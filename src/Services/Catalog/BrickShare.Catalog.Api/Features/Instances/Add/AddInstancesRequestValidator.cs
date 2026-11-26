using FluentValidation;

namespace BrickShare.Catalog.Api.Features.Instances.Add;

public sealed class AddInstancesRequestValidator : AbstractValidator<AddInstancesRequest>
{
    public AddInstancesRequestValidator()
    {
        RuleFor(request => request.Quantity)
            .GreaterThanOrEqualTo(1);
    }
}