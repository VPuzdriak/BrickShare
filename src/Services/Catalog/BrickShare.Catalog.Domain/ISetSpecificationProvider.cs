using System.Threading;
using System.Threading.Tasks;

namespace BrickShare.Catalog.Domain;

public interface ISetSpecificationProvider
{
    Task<SetSpecification?> FindAsync(RebrickableSetNumber setNumber, CancellationToken cancellationToken);
}