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

    public static ProblemHttpResult SomeNotFound() =>
        TypedResults.Problem(
            title: "Instance not found",
            detail: "At least one instance in the request does not exist.",
            statusCode: StatusCodes.Status404NotFound);

    public static ProblemHttpResult UnknownInventoryCode(string inventoryCode) =>
        TypedResults.Problem(
            title: "Instance not found",
            detail: $"No instance has inventory code {inventoryCode}.",
            statusCode: StatusCodes.Status404NotFound);

    public static ProblemHttpResult InvalidInventoryCode(string inventoryCode) =>
        TypedResults.Problem(
            title: "Invalid inventory code",
            detail: $"'{inventoryCode}' is not an inventory code. Check the code printed below the QR code.",
            statusCode: StatusCodes.Status400BadRequest);
}