using System.Linq;
using System.Text.Json;

using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BrickShare.Catalog.Api.Infrastructure.Validation;

public static class ValidationProblems
{
    public static ValidationProblem From(ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(
                failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName ?? string.Empty),
                failure => failure.ErrorMessage)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return TypedResults.ValidationProblem(errors);
    }
}