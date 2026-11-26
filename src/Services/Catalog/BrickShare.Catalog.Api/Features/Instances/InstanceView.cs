using System;
using System.Linq;
using System.Linq.Expressions;

using BrickShare.Catalog.Api.Infrastructure.Persistence;
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Features.Instances;

public sealed record InstanceView(
    Guid Id,
    InventoryCode InventoryCode,
    string Condition,
    string Status,
    Guid SetId,
    string SetName,
    LegoSetNumber LegoSetNumber)
{
    public static IQueryable<InstanceView> Query(
        CatalogDbContext dbContext,
        Expression<Func<SetInstance, bool>> filter) =>
        dbContext.SetInstances.Where(filter).Join(
            dbContext.LegoSets,
            instance => instance.SetId,
            legoSet => legoSet.Id,
            (instance, legoSet) => new InstanceView(
                instance.Id,
                instance.InventoryCode,
                instance.Condition,
                instance.Status,
                legoSet.Id,
                legoSet.Name,
                legoSet.LegoSetNumber));
}