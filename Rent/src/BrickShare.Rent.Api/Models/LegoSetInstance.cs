namespace BrickShare.Rent.Api.Models;

internal sealed class LegoSetInstance {
  public required Guid Id { get; init; }
  public required Guid SetId { get; init; }
  public required decimal PricePerDay { get; init; }
  public required int MinimalRentalDays { get; init; }
  public required int ConditionScore { get; init; }
  public required RentalStatus RentalStatus { get; init; }
}
