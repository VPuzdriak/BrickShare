using BrickShare.Rent.Api.Models;

namespace BrickShare.Rent.Api.Features.LegoSetInstances.Add;

internal static class AddLegoSetInstanceEndpoint {
  internal const string Route = "/add";

  public static void MapAddLegoSetInstance(this RouteGroupBuilder group) {
    group.MapPost(Route, async (AddLegoSetInstanceRequest request, AddLegoInstanceHandler handler, CancellationToken ct) => {
      LegoSetInstance instance = await handler.HandleAsync(
        new AddLegoInstance(request.SetId, request.PricePerDay, request.MinimalRentalDays, request.ConditionScore),
        ct);

      var dto = new LegoInstanceDto(
        instance.Id,
        instance.SetId,
        instance.PricePerDay,
        instance.MinimalRentalDays,
        instance.ConditionScore,
        instance.RentalStatus);

      return Results.Created($"{LegoSetInstancesEndpoints.RoutePrefix}/{instance.Id}", dto);
    });
  }
}

internal sealed record AddLegoSetInstanceRequest(
  Guid SetId,
  decimal PricePerDay,
  int MinimalRentalDays,
  int ConditionScore
);

internal sealed record LegoInstanceDto(
  Guid Id,
  Guid SetId,
  decimal PricePerDay,
  int MinimalRentalDays,
  int ConditionScore,
  RentalStatus RentalStatus
);
