using System;

namespace BrickShare.SharedKernel;

public abstract class Entity<TId>(TId id)
    where TId : notnull
{
    public TId Id { get; } = id;

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other
        && GetType() == other.GetType()
        && Id.Equals(other.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}