using System;

using BrickShare.Customers.Domain;

namespace BrickShare.Customers.Api.Features.Customers.BanCustomer;

public sealed record BanResponse(
    Guid Id,
    BanReason Reason,
    string Comment,
    string BannedBy,
    DateTimeOffset From,
    DateTimeOffset? Until,
    DateTimeOffset? LiftedAt)
{
    public static BanResponse Map(Ban ban) =>
        new(ban.Id, ban.Reason, ban.Comment, ban.BannedBy, ban.From, ban.Until, ban.LiftedAt);
}