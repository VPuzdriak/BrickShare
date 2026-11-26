using System;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Domain;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrickShare.Catalog.Api.Features.Sets.Lookup;

public static class LookupSet
{
    public static void MapLookupSet(this RouteGroupBuilder sets) =>
        sets.MapGet("/lookup/{setNumber}", Handle);

    private static async Task<Results<Ok<SetDraftResponse>, ProblemHttpResult>> Handle(
        string setNumber, ISetSpecificationProvider specifications, CancellationToken cancellationToken)
    {
        var parsed = RebrickableSetNumber.Parse(setNumber);

        if (parsed.IsFailure)
        {
            return SetProblems.InvalidSetNumber(setNumber);
        }

        SetSpecification? specification;

        try
        {
            specification = await specifications.FindAsync(parsed.Value, cancellationToken);
        }
        catch (SetSpecificationUnavailableException)
        {
            return SetProblems.RebrickableUnavailable();
        }

        if (specification is null)
        {
            return SetProblems.UnknownSetNumber(setNumber);
        }

        return TypedResults.Ok(SetDraftResponse.From(specification));
    }
}

public sealed record SetDraftResponse(
    string LegoSetNumber,
    string RebrickableSetNumber,
    string Name,
    int? Year,
    int PieceCount,
    int MinifigureCount,
    int RebrickableThemeId,
    string ThemeName,
    Uri? ImageUrl,
    Uri PageUrl)
{
    public static SetDraftResponse From(SetSpecification specification) => new(
        specification.RebrickableSetNumber.LegoSetNumber.Value,
        specification.RebrickableSetNumber.Value,
        specification.Name,
        specification.Year,
        specification.PieceCount,
        specification.MinifigureCount,
        specification.Theme.TopLevel.Id,
        specification.Theme.TopLevel.Name,
        specification.ImageUrl,
        specification.PageUrl);
}