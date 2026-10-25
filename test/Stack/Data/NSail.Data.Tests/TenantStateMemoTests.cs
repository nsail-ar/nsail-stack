// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

namespace NSail.Data.Tests;

/// <summary>What the memo will answer WITHOUT reading, which is what the callers that cannot
/// await derive visibility from. It has to tell "nothing is held" from "a row is held and it is
/// null" — the second is the answer almost every tenant has, and collapsing the two would make
/// every unwritten tenant look like one nobody has looked up yet.</summary>
public sealed class TenantStateMemoTests
{
    static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    static TenantStateMemo Memo(WoundClock clock, int seconds = 120)
    {
        return new TenantStateMemo(new TenancyOptions { StateCacheSeconds = seconds }, clock);
    }

    [Fact]
    public void Nothing_is_held_before_the_first_read()
    {
        Assert.False(Memo(new WoundClock()).Holds(Tenant, out var state));
        Assert.Null(state);
    }

    [Fact]
    public async Task A_row_that_was_read_is_held_with_what_it_carries()
    {
        var clock = new WoundClock();
        var memo = Memo(clock);

        await memo.Of(Tenant, _ => Task.FromResult<TenantState?>(new TenantState { TenantId = Tenant, Plan = "max" }));

        Assert.True(memo.Holds(Tenant, out var state));
        Assert.Equal("max", state!.Plan);
    }

    /// <summary>An absent row is remembered too, and saying so is the whole point: a tenant
    /// nobody has written is held as "looked up, and there is nothing".</summary>
    [Fact]
    public async Task An_absent_row_is_held_as_an_answer_and_not_as_silence()
    {
        var clock = new WoundClock();
        var memo = Memo(clock);

        await memo.Of(Tenant, _ => Task.FromResult<TenantState?>(null));

        Assert.True(memo.Holds(Tenant, out var state));
        Assert.Null(state);
    }

    [Fact]
    public async Task What_the_window_has_closed_on_is_no_longer_held()
    {
        var clock = new WoundClock();
        var memo = Memo(clock, seconds: 60);

        await memo.Of(Tenant, _ => Task.FromResult<TenantState?>(new TenantState { TenantId = Tenant }));

        clock.Advance(TimeSpan.FromSeconds(61));

        Assert.False(memo.Holds(Tenant, out _));
    }

    [Fact]
    public async Task One_tenant_is_not_the_other()
    {
        var memo = Memo(new WoundClock());

        await memo.Of(Tenant, _ => Task.FromResult<TenantState?>(new TenantState { TenantId = Tenant }));

        Assert.False(memo.Holds(Guid.Parse("22222222-2222-2222-2222-222222222222"), out _));
    }
}

// The memo's window is the only thing this suite has to move, and waiting it out would cost the
// setting's own two minutes.
sealed class WoundClock : TimeProvider
{
    DateTimeOffset _now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void Advance(TimeSpan span)
    {
        _now += span;
    }
}
