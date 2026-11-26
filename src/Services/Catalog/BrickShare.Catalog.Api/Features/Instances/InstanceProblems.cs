using System;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BrickShare.Catalog.Api.Features.Instances;

public static class InstanceProblems
{
    public static ProblemHttpResult NotFound(Guid id) =>
        TypedResults.Problem(
            title: "Instance not found",
            detail: $"No instance with id {id} exists.",
            statusCode: StatusCodes.Status404NotFound);
}