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
