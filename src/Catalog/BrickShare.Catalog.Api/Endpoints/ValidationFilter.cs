using FluentValidation;
using FluentValidation.Results;

namespace BrickShare.Catalog.Api.Endpoints;

/// <summary>
/// Validates the single <typeparamref name="TRequest"/> argument of an endpoint before its handler
/// runs, and answers a 400 with RFC 9457 if it does not pass.
/// </summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Resolved from RequestServices rather than through a constructor. A validator is scoped and
        // a filter is shared machinery, and pulling a scoped service in per request is the version
        // of this that has no lifetime question to answer.
        IValidator<TRequest> validator =
            context.HttpContext.RequestServices.GetRequiredService<IValidator<TRequest>>();

        TRequest? request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        // No argument of that type means the filter was attached to the wrong endpoint. Throwing is
        // right: the alternative is letting every request through unvalidated and never saying so.
        if (request is null)
        {
            throw new InvalidOperationException(
                $"This endpoint has no {typeof(TRequest).Name} argument to validate. "
                + "Check the AddEndpointFilter call against the handler's parameters.");
        }

        ValidationResult validation =
            await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        return validation.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(validation.ToDictionary());
    }
}
