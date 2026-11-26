using System;

using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Features.Instances;

public sealed record InstanceResponse(Guid Id, string InventoryCode, string Condition, string Status)
{
    public static InstanceResponse From(SetInstance instance) => new(
        instance.Id,
        instance.InventoryCode.ToString(),
        instance.Condition,
        instance.Status);
}