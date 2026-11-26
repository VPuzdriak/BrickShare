using System;
using System.Collections.Generic;

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

    public static ProblemHttpResult UnknownSetNumber(string setNumber) =>
        TypedResults.Problem(
            title: "Set not found",
            detail: $"Rebrickable has no set with number {setNumber}.",
            statusCode: StatusCodes.Status404NotFound);

    public static ProblemHttpResult RebrickableSetAlreadyExists(Guid existingSetId) =>
        TypedResults.Problem(
            title: "Set already exists",
            detail: "A set with this Rebrickable reference is already in the catalog.",
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["existingSetId"] = existingSetId });

    public static ProblemHttpResult NoRebrickableReference(Guid id) =>
        TypedResults.Problem(
            title: "No Rebrickable reference",
            detail: $"Set {id} was not created from a Rebrickable draft, so its specifications cannot be refreshed.",
            statusCode: StatusCodes.Status409Conflict);

    public static ProblemHttpResult InvalidSetNumber(string setNumber) =>
        TypedResults.Problem(
            title: "Invalid set number",
            detail: $"'{setNumber}' is not a set number. Use digits, optionally followed by a dash and a variant number, for example 10294 or 10294-2.",
            statusCode: StatusCodes.Status400BadRequest);

    public static ProblemHttpResult RebrickableUnavailable() =>
        TypedResults.Problem(
            title: "Rebrickable is unavailable",
            detail: "The set lookup is not available right now. Try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
}