using BrickShare.Rent.Api.Features.LegoSetInstances;

namespace BrickShare.Rent.Api.Endpoints;

internal static class EndpointsExtensions {
  public static void MapEndpoints(this IEndpointRouteBuilder app) {
    app.MapGroup(LegoSetInstancesEndpoints.RoutePrefix)
      .WithTags("Lego Set Instances")
      .AddEndpointFilter<ValidationEndpointFilter>()
      .MapLegoSetInstancesEndpoints();
  }
}
