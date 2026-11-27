using System;

using BrickShare.SharedKernel;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BrickShare.Customers.Api.Features.Customers;

public static class CustomerProblems
{
    public static ProblemHttpResult CustomerNotFound(Guid id) =>
        TypedResults.Problem(
            title: "Customer not found",
            detail: $"No customer with id {id} exists.",
            statusCode: StatusCodes.Status404NotFound);

    public static ProblemHttpResult InvalidProfile(DomainError error) =>
        TypedResults.Problem(
            title: "Invalid profile",
            detail: error.Message,
            statusCode: StatusCodes.Status400BadRequest);

    public static ProblemHttpResult InvalidBan(DomainError error) =>
        TypedResults.Problem(
            title: "Invalid ban",
            detail: error.Message,
            statusCode: StatusCodes.Status400BadRequest);

    public static ProblemHttpResult BanNotFound(Guid banId) =>
        TypedResults.Problem(
            title: "Ban not found",
            detail: $"No ban with id {banId} exists for this customer.",
            statusCode: StatusCodes.Status404NotFound);

    public static ProblemHttpResult BanAlreadyLifted(Guid banId) =>
        TypedResults.Problem(
            title: "Ban already lifted",
            detail: $"The ban {banId} was lifted before. Only an active ban can be lifted.",
            statusCode: StatusCodes.Status409Conflict);

    public static ProblemHttpResult SearchTextRequired() =>
        TypedResults.Problem(
            title: "Search text required",
            detail: "Send the text to find in the search query parameter, for example ?search=ada.",
            statusCode: StatusCodes.Status400BadRequest);
}