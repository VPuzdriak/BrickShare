using BrickShare.Rent.Api.Data;
using BrickShare.Rent.Api.Models;

namespace BrickShare.Rent.Api.Features.LegoSetInstances.Add;

internal sealed record AddLegoInstance(Guid SetId, decimal PricePerDay, int MinimalRentalDays, int ConditionScore);

internal sealed class AddLegoInstanceHandler(RentalDbContext dbContext) {
  public async Task<LegoSetInstance> HandleAsync(AddLegoInstance request, CancellationToken ct) {
    var instance = new LegoSetInstance {
      Id = Guid.NewGuid(),
      SetId = request.SetId,
      PricePerDay = request.PricePerDay,
      MinimalRentalDays = request.MinimalRentalDays,
      ConditionScore = request.ConditionScore,
      RentalStatus = RentalStatus.Available
    };

    await dbContext.LegoSetInstances.AddAsync(instance, ct);
    await dbContext.SaveChangesAsync(ct);

    return instance;
  }
}
