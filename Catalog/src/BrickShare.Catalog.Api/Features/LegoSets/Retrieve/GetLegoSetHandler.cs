using BrickShare.Catalog.Api.Data;
using BrickShare.Catalog.Api.Models;

using ErrorOr;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.LegoSets.Retrieve;

internal sealed class GetLegoSetHandler(CatalogDbContext dbContext) {
  public async Task<ErrorOr<LegoSet>> HandleAsync(Guid id, CancellationToken cancellationToken) {
    var set = await dbContext.LegoSets
      .AsNoTracking()
      .Include(s => s.Theme)
      .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    if (set is null) {
      return Error.NotFound("LegoSet.NotFound", $"Lego set with ID '{id}' was not found.");
    }

    return set;
  }
}

