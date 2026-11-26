using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrickShare.Catalog.Domain;

public interface IInventoryCodeSequence
{
    Task<IReadOnlyList<long>> NextValuesAsync(int count, CancellationToken cancellationToken);
}