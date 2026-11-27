using System;

using BrickShare.SharedKernel;

namespace BrickShare.Customers.Domain;

public sealed class Ban : Entity<Guid>
{
    public const string SystemActor = "system";

    internal Ban(Guid id, BanReason reason, string comment, string bannedBy, DateTimeOffset from, DateTimeOffset? until)
        : base(id)
    {
        Reason = reason;
        Comment = comment;
        BannedBy = bannedBy;
        From = from;
        Until = until;
    }

    public BanReason Reason { get; }

    public string Comment { get; }

    public string BannedBy { get; }

    public DateTimeOffset From { get; }

    public DateTimeOffset? Until { get; }

    public DateTimeOffset? LiftedAt { get; private set; }

    public bool IsActive => LiftedAt is null;

    internal void Lift(DateTimeOffset now) => LiftedAt = now;
}