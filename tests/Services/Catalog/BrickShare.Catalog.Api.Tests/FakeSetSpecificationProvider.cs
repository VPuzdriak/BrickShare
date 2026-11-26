using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Tests;

public sealed class FakeSetSpecificationProvider : ISetSpecificationProvider
{
    private readonly Dictionary<string, SetSpecification> _specifications = [];

    public bool IsUnavailable { get; set; }

    public void Add(SetSpecification specification) =>
        _specifications[specification.RebrickableSetNumber.Value] = specification;

    public Task<SetSpecification?> FindAsync(RebrickableSetNumber setNumber, CancellationToken cancellationToken)
    {
        if (IsUnavailable)
        {
            throw new SetSpecificationUnavailableException("Rebrickable is unavailable.");
        }

        return Task.FromResult(_specifications.GetValueOrDefault(setNumber.Value));
    }
}