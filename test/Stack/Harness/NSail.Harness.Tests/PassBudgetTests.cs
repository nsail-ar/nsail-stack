// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

// An idle harness passes every IdlePollSeconds, and every pass reads the whole board: what a pass bills
// on GitHub's REST quota, times about 120 passes an hour, is the token's hourly spend. A read whose
// answer has not moved since the last pass is sent with its ETag and answered 304, which GitHub does
// not count; only what changed is billed.
[Collection(nameof(PassFailureTests))]
public sealed class PassBudgetTests
{
    const int Prs = 10;
    static readonly int[] Locked = [21, 22, 23, 24, 25, 26];

    [Fact]
    public async Task APassOverAnUnchangedBoardBillsNoRestCall()
    {
        var hub = new FakeGitHub { Prs = Prs, Locked = Locked };
        using var harness = new FakeGitHub.Harness(hub);
        var first = await harness.Start();
        Assert.Null(first.Error);
        var warm = hub.Count;

        harness.Loop.Wake();
        var second = await harness.NextPass(first);

        Assert.Null(second.Error);
        Assert.Empty(hub.Billed(warm));
    }

    [Fact]
    public async Task APassBillsOnlyTheReadWhoseAnswerMoved()
    {
        var hub = new FakeGitHub { Prs = Prs, Locked = Locked };
        using var harness = new FakeGitHub.Harness(hub);
        var first = await harness.Start();
        var warm = hub.Count;

        hub.Main = "m2";
        harness.Loop.Wake();
        await harness.NextPass(first);

        Assert.Equal([$"GET /repos/{FakeGitHub.Repo}/commits/main"], hub.Billed(warm));
    }

    // The first pass reads everything once: four listings of the running workflows, main's head, the
    // newest PR runs and one per quiet head, the releases, and the label events of each locked story.
    [Fact]
    public async Task TheFirstPassReadsEachCallSiteOnce()
    {
        var hub = new FakeGitHub { Prs = Prs, Locked = Locked };
        using var harness = new FakeGitHub.Harness(hub);

        await harness.Start();

        var billed = hub.Billed();
        Assert.Equal(4, billed.Count(c => c.Contains("actions/runs?status=", StringComparison.Ordinal)));
        Assert.Equal(Prs, billed.Count(c => c.Contains("actions/runs?head_sha=", StringComparison.Ordinal)));
        Assert.Equal(Locked.Length, billed.Count(c => c.Contains("/events", StringComparison.Ordinal)));
    }
}
