// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

public class GatePlannerTests
{
    static readonly PlannerOptions WithGate = new(Gate: true);

    static List<string> Lines(Fixture f)
    {
        return SampleWorkers.Plan(f.Build(), WithGate).Items.Select(i => i.ToString()).ToList();
    }

    [Fact]
    public void AHeadWithNoGateIsGatedWhateverActionsSays()
    {
        Assert.Equal(["gate PR #10 (story #1)"], Lines(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Pending)));
        Assert.Equal(["gate PR #10 (story #1)"], Lines(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Red)));
    }

    [Fact]
    public void AGreenGateGoesToReviewAndARedOneToARound()
    {
        Assert.Equal(["review PR #10 (story #1)"], Lines(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Red, gate: CiState.Green)));
        Assert.Equal(["dev #1 (round (PR #10: gate red))"], Lines(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Green, gate: CiState.Red)));
    }

    [Fact]
    public void AGateRunningHoldsItsStory()
    {
        Assert.Empty(Lines(new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending)));
    }

    [Fact]
    public void AConflictGoesStraightToARoundWithoutAGate()
    {
        Assert.Equal(["dev #1 (round (PR #10: CI conflict))"], Lines(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Conflict)));
    }

    [Fact]
    public void AStackPrKeepsItsOwnCi()
    {
        Assert.Equal(["dev #1 (round (framework PR #10: CI red))"],
            Lines(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Red, repo: Fixture.Stack)));
    }

    // nsail-stack PR #240: its one failed test failed on main's newest CI too.
    static CiFailure Failing(params string[] tests)
    {
        return CiFailure.Of([("build", "Test", string.Join('\n', tests.Select(t => $"2026-11-08T07:04:01.1234567Z   Failed {t} [12 ms]")))]);
    }

    const string Bound = "NSail.Telemetry.Tests.FailureLogTests.The_bound_is_the_period_the_caller_asked_for";

    [Fact]
    public void AStackPrRedOnlyWhereMainsOwnCiIsRedWaitsForMainToMove()
    {
        var pr = Failing(Bound);
        Assert.True(pr.SharedWith(Failing(Bound, "NSail.A.T.Other")));

        var plan = SampleWorkers.Plan(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Red, repo: Fixture.Stack, shared: true).Build(), WithGate);
        Assert.Empty(plan.Items);
        Assert.Contains(plan.Notes, n => n.StartsWith("#1: framework PR #10 red only where main's own CI is red", StringComparison.Ordinal));
    }

    [Fact]
    public void AHeldStorysSharedRedSaysNothing()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(1, "In-Progress", "stalled").Pr(1, 10, CiState.Red, repo: Fixture.Stack, shared: true).Build(), WithGate);
        Assert.DoesNotContain(plan.Notes, n => n.Contains("red only where main's own CI is red", StringComparison.Ordinal));
    }

    [Fact]
    public void OnceMainsNewestCiNoLongerSharesTheRedTheRoundGoes()
    {
        var pr = Failing(Bound);
        Assert.False(pr.SharedWith(Failing("NSail.A.T.Other")));
        Assert.False(pr.SharedWith(null));
        Assert.False(Failing(Bound, "NSail.A.T.Mine").SharedWith(Failing(Bound)));
        // A red that names nothing (no test, no build error) is never main's.
        Assert.False(CiFailure.Of([("build", "Restore", "##[error]Process completed with exit code 1.")]).SharedWith(Failing(Bound)));

        Assert.Equal(["dev #1 (round (framework PR #10: CI red))"],
            Lines(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Red, repo: Fixture.Stack, shared: false)));
    }

    // The pass's read of a Stack PR red at its head, its own CI failing `pr` and main's newest failing `main`.
    static async Task<IReadOnlyList<string>> PlanOfRead(string sha, string[] pr, string[]? main)
    {
        var open = new OpenPr(240, "app#8: the fix", "stack/app-8", sha, CiState.Red, Fixture.Stack, "now", HeadRepo: Fixture.Stack);
        var framework = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => Fixture.Stack,
            ["OpenPrs"] = _ => Task.FromResult<IReadOnlyList<OpenPr>>([open]),
            ["Failure"] = a => Task.FromResult<CiFailure?>((string)a[0]! == sha ? Failing(pr) : null),
            ["MainFailure"] = _ => Task.FromResult(main is null ? null : (CiFailure?)Failing(main)),
        });
        var home = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>> { ["get_Name"] = _ => Fixture.Repo });
        var options = SampleWorkers.Options;
        var repos = new RepositorySet(options.Repositories.Select(o => (o.Name == Fixture.Stack ? framework : home, o)).ToList());
        var read = await new SnapshotReader(new MemoryBoard(), repos, null!, options).ReadPrs(framework, "main", CancellationToken.None);
        var plan = SampleWorkers.Plan(new Fixture().Story(8, "In-Progress").Build() with { Prs = read }, WithGate);
        return [.. plan.Items.Select(i => i.ToString()), .. plan.Notes.Where(n => n.StartsWith("#8:", StringComparison.Ordinal))];
    }

    [Fact]
    public async Task TheReadComparesThePrsFailuresAgainstMainsNotTheOtherWayRound()
    {
        // PR #240 fails one test; main's newest CI fails it and another: main shares the whole red.
        Assert.Equal(["#8: framework PR #240 red only where main's own CI is red — no round until main moves"],
            await PlanOfRead("aaaa000000000001", [Bound], [Bound, "NSail.A.T.Other"]));

        // The PR fails a test main does not: its own red, a round.
        Assert.Equal(["dev #8 (round (framework PR #240: CI red))"],
            await PlanOfRead("aaaa000000000002", [Bound, "NSail.A.T.Mine"], [Bound]));

        // Main moved: its newest completed CI fails nothing, the round goes.
        Assert.Equal(["dev #8 (round (framework PR #240: CI red))"],
            await PlanOfRead("aaaa000000000003", [Bound], null));
    }

    [Fact]
    public void WithTheGateOffActionsCiDecides()
    {
        Assert.Equal(["review PR #10 (story #1)"],
            SampleWorkers.Plan(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Green).Build()).Items.Select(i => i.ToString()));
    }

    [Fact]
    public void AGateNeedsABuildSlotAndNoAgent()
    {
        var gate = new WorkItem(WorkKind.Gate, 1, 10, "gate", "x");
        var picked = Capacity.Pick([gate], new Dictionary<Resource, int> { [Resource.Agent] = 9 }, new Limits(2, 3, 1));

        Assert.Single(picked);
    }
}
