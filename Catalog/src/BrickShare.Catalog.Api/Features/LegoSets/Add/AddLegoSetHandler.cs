using BrickShare.Catalog.Api.Data;
using BrickShare.Catalog.Api.Models;

using ErrorOr;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Features.LegoSets.Add;

internal sealed record AddLegoSet(string Name, string LegoId, DateOnly ReleaseDate, int NumberOfParts, int AgeFrom, Guid ThemeId);

internal sealed class AddLegoSetHandler(CatalogDbContext dbContext) {
  public async Task<ErrorOr<LegoSet>> HandleAsync(AddLegoSet command, CancellationToken cancellationToken) {
    var existing = await dbContext.LegoSets
      .AsNoTracking()
      .FirstOrDefaultAsync(s => s.LegoId == command.LegoId, cancellationToken);

    if (existing is not null) {
      return Error.Conflict("LegoSet.Duplicate", $"A Lego set with Lego ID '{command.LegoId}' already exists.");
    }

    var theme = await dbContext.LegoThemes
      .FirstOrDefaultAsync(t => t.Id == command.ThemeId, cancellationToken);

    if (theme is null) {
      return Error.NotFound("LegoTheme.NotFound", $"Theme with ID '{command.ThemeId}' was not found.");
    }

    var set = new LegoSet {
      Id = Guid.NewGuid(),
      Name = command.Name,
      LegoId = command.LegoId,
      ReleaseDate = command.ReleaseDate,
      NumberOfParts = command.NumberOfParts,
      AgeFrom = command.AgeFrom,
      ThemeId = command.ThemeId,
      Theme = theme
    };

    await dbContext.AddAsync(set, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    return set;
  }
}
