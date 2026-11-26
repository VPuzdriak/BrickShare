using System;

using BrickShare.SharedKernel;

namespace BrickShare.Catalog.Domain;

public sealed class SetInstance : AggregateRoot<Guid>
{
    private SetInstance()
        : this(Guid.Empty, Guid.Empty, null!, InstanceCondition.New)
    {
    }

    public SetInstance(Guid id, Guid setId, InventoryCode inventoryCode, string condition)
        : base(id)
    {
        SetId = setId;
        InventoryCode = inventoryCode;
        Condition = condition;
        Status = InstanceStatus.Available;
    }

    public Guid SetId { get; private set; }

    public InventoryCode InventoryCode { get; private set; }

    public string Condition { get; private set; }

    public string Status { get; private set; }

    public void ChangeCondition(string condition) => Condition = condition;

    public void ChangeStatus(string status) => Status = status;
}