using BrickShare.Rent.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Rent.Api.Data;

internal sealed class RentalDbContext(DbContextOptions<RentalDbContext> options) : DbContext(options) {
  public required DbSet<LegoSetInstance> LegoSetInstances { get; init; }
}
