// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

public class BuildLeasesTests
{
    static readonly TimeSpan Soon = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task TurnsUpToTheLimitAreGrantedAtOnce()
    {
        var leases = new BuildLeases(2);

        await leases.Acquire("a", 5, default);
        await leases.Acquire("b", 5, default);

        Assert.Equal(2, leases.Held);
        Assert.Equal(0, leases.Waiting);
    }

    [Fact]
    public async Task AFreedTurnGoesToTheMostUrgentWaiterNotTheFirstToAsk()
    {
        var leases = new BuildLeases(1);
        var first = await leases.Acquire("holder", 5, default);
        var lazy = leases.Acquire("rest", 9, default);
        var urgent = leases.Acquire("red", 0, default);
        var middle = leases.Acquire("round", 3, default);

        Assert.Equal(3, leases.Waiting);
        leases.Release(first.Id);
        Assert.Equal("red", (await urgent.WaitAsync(Soon)).Holder);
        Assert.False(lazy.IsCompleted);
        Assert.False(middle.IsCompleted);
    }

    [Fact]
    public async Task EqualUrgencyKeepsTheOrderOfArrival()
    {
        var leases = new BuildLeases(1);
        var held = await leases.Acquire("holder", 1, default);
        var a = leases.Acquire("a", 4, default);
        var b = leases.Acquire("b", 4, default);

        leases.Release(held.Id);

        Assert.Equal("a", (await a.WaitAsync(Soon)).Holder);
        Assert.False(b.IsCompleted);
    }

    [Fact]
    public async Task ACancelledWaitLeavesTheLineAndTakesNothing()
    {
        var leases = new BuildLeases(1);
        var held = await leases.Acquire("holder", 1, default);
        using var cts = new CancellationTokenSource();
        var gone = leases.Acquire("gone", 0, cts.Token);
        var stays = leases.Acquire("stays", 5, default);

        await cts.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(() => gone);
        leases.Release(held.Id);

        Assert.Equal("stays", (await stays.WaitAsync(Soon)).Holder);
        Assert.Equal(1, leases.Held);
    }

    [Fact]
    public async Task ReleasingTwiceIsHarmless()
    {
        var leases = new BuildLeases(1);
        var l = await leases.Acquire("a", 1, default);

        Assert.True(leases.Release(l.Id));
        Assert.False(leases.Release(l.Id));
        Assert.Equal(0, leases.Held);
    }

    [Fact]
    public void ALimitBelowOneStillGrantsOne()
    {
        Assert.Equal(1, new BuildLeases(0).Limit);
    }

    [Fact]
    public async Task ASweepLeavesATurnInsideTheLongestStepAndReturnsADeadOne()
    {
        var leases = new BuildLeases(1);
        await leases.Acquire("release", 5, default);
        var age = HarnessLoop.TurnAge(SampleWorkers.Workers);

        Assert.True(age >= TimeSpan.FromMinutes(150));
        Assert.True(age >= TimeSpan.FromMinutes(SampleWorkers.Workers.Where(w => w.Kind == WorkKind.Main).Max(w => (w.Options.Minutes ?? w.Type.Minutes) * 2)));
        Assert.Equal(0, leases.Sweep(age, DateTimeOffset.UtcNow.AddMinutes(90)));
        Assert.Equal(1, leases.Held);
        Assert.Equal(1, leases.Sweep(age, DateTimeOffset.UtcNow.AddMinutes(400)));
        Assert.Equal(0, leases.Held);
    }

    [Fact]
    public void ACutHeldByAnotherHarnessIsNotDueUntilItsClaimLapsesOrIsLetGo()
    {
        var worker = SampleWorkers.Workers.First(w => w.Kind == WorkKind.Release && w.Options.Product is not null);
        var p = worker.Options.Product!.ToLowerInvariant();
        var fired = DateTimeOffset.UtcNow.AddMinutes(-200);
        var now = fired.AddMinutes(200);
        StatusMark Held(bool ok, int minutes) => new("c", Markers.HeldContext(p), ok, fired.AddMinutes(minutes));

        Assert.True(HarnessLoop.CutHeld([Held(false, 150)], worker, fired, now));
        Assert.False(HarnessLoop.CutHeld([Held(false, 10)], worker, fired, now));
        Assert.False(HarnessLoop.CutHeld([Held(false, 150), Held(true, 160)], worker, fired, now));
        Assert.False(HarnessLoop.CutHeld([], worker, fired, now));
    }
}
