using System;
using System.Collections.Generic;

using BrickShare.SharedKernel;

namespace BrickShare.Customers.Domain;

public sealed class Customer : AggregateRoot<Guid>
{
    private const int MaxDisplayNameLength = 100;
    private const int MaxPhoneLength = 32;
    private const string LostSetComment = "The rented set was lost.";

    private static readonly DomainError DisplayNameRequired = new(
        "customer.display_name_required",
        "A display name is required.");

    private static readonly DomainError DisplayNameTooLong = new(
        "customer.display_name_too_long",
        "A display name has at most 100 characters.");

    private static readonly DomainError PhoneTooLong = new(
        "customer.phone_too_long",
        "A phone number has at most 32 characters.");

    private static readonly DomainError BanCommentRequired = new(
        "customer.ban_comment_required",
        "A ban needs a comment.");

    private static readonly DomainError BanUntilNotInFuture = new(
        "customer.ban_until_not_in_future",
        "A temporary ban must end after the current time.");

    private static readonly DomainError BanNotFound = new(
        "customer.ban_not_found",
        "The ban does not exist.");

    private static readonly DomainError BanAlreadyLifted = new(
        "customer.ban_already_lifted",
        "The ban is already lifted.");

    private readonly List<Ban> _bans = [];

    private Customer()
        : this(Guid.Empty, string.Empty, string.Empty, default)
    {
    }

    public Customer(Guid id, string email, string displayName, DateTimeOffset createdAt)
        : base(id)
    {
        Email = email;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? Phone { get; private set; }

    public CustomerStatus Status { get; private set; } = CustomerStatus.Active;

    public IReadOnlyList<Ban> Bans => _bans.AsReadOnly();

    public Result UpdateProfile(string? displayName, string? phone)
    {
        var name = displayName?.Trim() ?? string.Empty;
        var number = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

        if (name.Length == 0)
        {
            return Result.Failure(DisplayNameRequired);
        }

        if (name.Length > MaxDisplayNameLength)
        {
            return Result.Failure(DisplayNameTooLong);
        }

        if (number?.Length > MaxPhoneLength)
        {
            return Result.Failure(PhoneTooLong);
        }

        DisplayName = name;
        Phone = number;

        return Result.Success();
    }

    public Result AddBan(BanReason reason, string? comment, string bannedBy, DateTimeOffset? until, DateTimeOffset now)
    {
        var text = comment?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return Result.Failure(BanCommentRequired);
        }

        if (until is { } end && end <= now)
        {
            return Result.Failure(BanUntilNotInFuture);
        }

        _bans.Add(new Ban(Guid.CreateVersion7(), reason, text, bannedBy, now, until));
        RefreshStatus();

        return Result.Success();
    }

    public Result LiftBan(Guid banId, DateTimeOffset now)
    {
        var ban = _bans.Find(candidate => candidate.Id == banId);

        if (ban is null)
        {
            return Result.Failure(BanNotFound);
        }

        if (!ban.IsActive)
        {
            return Result.Failure(BanAlreadyLifted);
        }

        ban.Lift(now);
        RefreshStatus();

        return Result.Success();
    }

    private void RefreshStatus() =>
        Status = _bans.Exists(candidate => candidate.IsActive) ? CustomerStatus.Banned : CustomerStatus.Active;

    public Result BanForLostSet(DateTimeOffset now) =>
        AddBan(BanReason.NonReturn, LostSetComment, Ban.SystemActor, until: null, now: now);
}