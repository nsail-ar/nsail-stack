// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Harness.Tests;

// An idle harness passes every IdlePollSeconds, and every pass reads the whole board: what a pass bills
// on GitHub's REST quota, times about 120 passes an hour, is the token's hourly spend. A read whose
// answer has not moved since the last pass is sent with its ETag and answered 304, which GitHub does
// not count; only what changed is billed.
[Collection(nameof(PassFailureTests))]
public sealed partial class PassBudgetTests
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

    // The first pass reads everything once, and each call site is pinned by its count: a read added to
    // the pass turns this red, even one whose ETag would leave the passes after it unbilled.
    [Fact]
    public async Task TheFirstPassReadsEachCallSiteOnce()
    {
        var hub = new FakeGitHub { Prs = Prs, Locked = Locked };
        using var harness = new FakeGitHub.Harness(hub);

        await harness.Start();

        var sites = hub.Billed().Select(c => Number().Replace(c[(c.IndexOf(' ', StringComparison.Ordinal) + 1)..], "#")).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        var api = $"/repos/{FakeGitHub.Repo}/";
        Assert.Equal(new Dictionary<string, int>
        {
            [$"{api}actions/runs?status=queued&per_page=#"] = 1,
            [$"{api}actions/runs?status=in_progress&per_page=#"] = 1,
            [$"{api}actions/runs?status=waiting&per_page=#"] = 1,
            [$"{api}actions/runs?status=pending&per_page=#"] = 1,
            [$"{api}commits/main"] = 1,
            [$"{api}actions/runs?event=pull_request&per_page=#"] = 1,
            [$"{api}actions/runs?head_sha=head#&event=pull_request&per_page=#"] = Prs,
            [$"{api}pulls/#/reviews?per_page=#"] = Prs,
            [$"{api}issues/#/comments?per_page=#"] = Prs,
            ["/user"] = 1,
            [$"{api}releases?per_page=#"] = 1,
            [$"{api}issues/#/events?per_page=#"] = Locked.Length,
            [$"{api}git/matching-refs/locks/app-"] = 1,
            [$"{api}git/tags/tag#x#"] = Locked.Length,
            [$"{api}git/ref/locks/app-#"] = Locked.Length,
        }, sites);
    }

    [GeneratedRegex("[0-9]+")]
    private static partial Regex Number();
}
