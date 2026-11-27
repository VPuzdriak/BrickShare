using BrickShare.SharedKernel;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BrickShare.Customers.Api.Features.Customers;

public static class CustomerProblems
{
    public static ProblemHttpResult InvalidProfile(DomainError error) =>
        TypedResults.Problem(
            title: "Invalid profile",
            detail: error.Message,
            statusCode: StatusCodes.Status400BadRequest);

    public static ProblemHttpResult SearchTextRequired() =>
        TypedResults.Problem(
            title: "Search text required",
            detail: "Send the text to find in the search query parameter, for example ?search=ada.",
            statusCode: StatusCodes.Status400BadRequest);
}