using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Catalog.Api.Infrastructure.Validation;

public sealed class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();

        if (request is null)
        {
            return await next(context);
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(
                failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName ?? string.Empty),
                failure => failure.ErrorMessage)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return TypedResults.ValidationProblem(errors);
    }
}