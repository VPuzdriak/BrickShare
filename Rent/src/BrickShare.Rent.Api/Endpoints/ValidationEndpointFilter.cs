using FluentValidation;

namespace BrickShare.Rent.Api.Endpoints;

internal sealed class ValidationEndpointFilter : IEndpointFilter {
  public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
    foreach (var argument in context.Arguments.Where(a => a is not null)) {
      if (argument is null) {
        continue;
      }

      var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
      if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator) {
        continue;
      }

      var validationContext = new ValidationContext<object>(argument);
      var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

      if (!result.IsValid) {
        return Results.ValidationProblem(result.ToDictionary());
      }
    }

    return await next(context);
  }
}
