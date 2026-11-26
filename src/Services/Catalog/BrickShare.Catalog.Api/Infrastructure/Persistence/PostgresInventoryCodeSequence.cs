using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

namespace BrickShare.Catalog.Api.Infrastructure.Persistence;

public sealed class PostgresInventoryCodeSequence(CatalogDbContext dbContext) : IInventoryCodeSequence
{
    public async Task<IReadOnlyList<long>> NextValuesAsync(int count, CancellationToken cancellationToken) =>
        await dbContext.Database
            .SqlQuery<long>($"SELECT nextval('inventory_code_seq') FROM generate_series(1, {count})")
            .ToListAsync(cancellationToken);
}