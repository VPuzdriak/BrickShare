using System;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BrickShare.Catalog.Api.Features.Sets;

public static class SetProblems
{
    public static ProblemHttpResult NotFound(Guid id) =>
        TypedResults.Problem(
            title: "Set not found",
            detail: $"No set with id {id} exists.",
            statusCode: StatusCodes.Status404NotFound);
}