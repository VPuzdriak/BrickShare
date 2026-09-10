using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Diagnostics;

namespace BrickShare.Catalog.Api;

/// <summary>
/// Turns a refused business rule into a 409 the client can read. Anything else is left alone,
/// so a genuine defect is still reported as the 500 it is.
/// </summary>
public sealed class DomainRuleViolationExceptionHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainRuleViolationException violation)
        {
            // False means "not mine". The next handler gets it, and if nobody claims it the
            // pipeline produces episode 22's 500 — the correct answer for a bug.
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = violation,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The catalog refused this change.",

                // Safe to put on the wire because the type says so. This message was written
                // for a person; an InvalidOperationException's was not.
                Detail = violation.Message
            }
        });
    }
}
