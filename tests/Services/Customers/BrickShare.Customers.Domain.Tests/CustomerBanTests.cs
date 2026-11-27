using System;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace BrickShare.Customers.Domain.Tests;

public class CustomerBanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ban_stores_the_ban_and_sets_the_status_to_banned()
    {
        var clock = new FakeTimeProvider(Now);
        var customer = CreateCustomer();
        var until = Now.AddDays(7);

        var result = customer.AddBan(BanReason.LateReturn, "Returned three days late.", "staff-1", until, clock.GetUtcNow());

        result.IsSuccess.Should().BeTrue();
        customer.Status.Should().Be(CustomerStatus.Banned);

        var ban = customer.Bans.Should().ContainSingle().Which;
        ban.Reason.Should().Be(BanReason.LateReturn);
        ban.Comment.Should().Be("Returned three days late.");
        ban.BannedBy.Should().Be("staff-1");
        ban.From.Should().Be(Now);
        ban.Until.Should().Be(until);
        ban.LiftedAt.Should().BeNull();
    }

    [Fact]
    public void Ban_fails_when_the_comment_is_blank()
    {
        var clock = new FakeTimeProvider(Now);
        var customer = CreateCustomer();

        var result = customer.AddBan(BanReason.Other, "   ", "staff-1", null, clock.GetUtcNow());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("customer.ban_comment_required");
        customer.Bans.Should().BeEmpty();
        customer.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public void Ban_fails_when_the_ban_does_not_end_after_now()
    {
        var clock = new FakeTimeProvider(Now);
        var customer = CreateCustomer();

        var result = customer.AddBan(BanReason.Other, "Staff note.", "staff-1", clock.GetUtcNow(), clock.GetUtcNow());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("customer.ban_until_not_in_future");
        customer.Bans.Should().BeEmpty();
    }

    [Fact]
    public void Lift_clears_the_banned_status_and_sets_the_lift_time()
    {
        var clock = new FakeTimeProvider(Now);
        var customer = CreateCustomer();
        customer.AddBan(BanReason.LateReturn, "Returned late.", "staff-1", null, clock.GetUtcNow());
        var banId = customer.Bans[0].Id;
        clock.Advance(TimeSpan.FromDays(2));

        var result = customer.LiftBan(banId, clock.GetUtcNow());

        result.IsSuccess.Should().BeTrue();
        customer.Bans[0].LiftedAt.Should().Be(Now.AddDays(2));
        customer.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public void Lift_keeps_the_customer_banned_while_another_ban_is_active()
    {
        var clock = new FakeTimeProvider(Now);
        var customer = CreateCustomer();
        customer.AddBan(BanReason.LateReturn, "Returned late.", "staff-1", null, clock.GetUtcNow());
        customer.AddBan(BanReason.Neglect, "Set not cleaned.", "staff-1", null, clock.GetUtcNow());
        var firstBanId = customer.Bans[0].Id;
        var secondBanId = customer.Bans[1].Id;

        customer.LiftBan(firstBanId, clock.GetUtcNow());
        customer.Status.Should().Be(CustomerStatus.Banned);

        customer.LiftBan(secondBanId, clock.GetUtcNow());
        customer.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public void Lift_fails_for_an_unknown_ban()
    {
        var clock = new FakeTimeProvider(Now);
        var customer = CreateCustomer();

        var result = customer.LiftBan(Guid.CreateVersion7(), clock.GetUtcNow());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("customer.ban_not_found");
    }

    [Fact]
    public void Lift_fails_when_the_ban_is_already_lifted()
    {
        var clock = new FakeTimeProvider(Now);
        var customer = CreateCustomer();
        customer.AddBan(BanReason.LateReturn, "Returned late.", "staff-1", null, clock.GetUtcNow());
        var banId = customer.Bans[0].Id;
        customer.LiftBan(banId, clock.GetUtcNow());

        var result = customer.LiftBan(banId, clock.GetUtcNow());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("customer.ban_already_lifted");
    }

    [Fact]
    public void Lost_set_bans_the_customer_automatically_for_non_return()
    {
        var clock = new FakeTimeProvider(Now);
        var customer = CreateCustomer();

        var result = customer.BanForLostSet(clock.GetUtcNow());

        result.IsSuccess.Should().BeTrue();
        customer.Status.Should().Be(CustomerStatus.Banned);

        var ban = customer.Bans.Should().ContainSingle().Which;
        ban.Reason.Should().Be(BanReason.NonReturn);
        ban.BannedBy.Should().Be("system");
        ban.Until.Should().BeNull();
    }

    private static Customer CreateCustomer() =>
        new(Guid.CreateVersion7(), "ada@example.test", "Ada Lovelace", Now);
}