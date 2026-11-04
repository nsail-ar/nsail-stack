// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// The bookkeeping the board needs beside the work: what GitHub Actions' event workflows did.
public class BoardKeepingTests
{
    static Plan Make(Fixture f)
    {
        return SampleWorkers.Plan(f.Build());
    }

    [Fact]
    public void AnIssueWithNoStageEntersBacklog()
    {
        var plan = Make(new Fixture().Story(1, "-"));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 1, Value: "Backlog" });
    }

    [Fact]
    public void ABlockedStoryGoesToAnalyzeWhenItsLastBlockerReachesMerged()
    {
        var plan = Make(new Fixture().Story(1, "Backlog", "blocked", blockers: [2]).Story(2, "Merged"));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 1, Value: "blocked" });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 1, Value: "Analyze" });
    }

    [Fact]
    public void ABlockedStoryWhoseBlockersClosedIsLiftedToo()
    {
        // A closed blocker leaves the open list; the total still says the story had blockers.
        var plan = Make(new Fixture().Story(1, "Backlog", "blocked", blockers: [], blockersTotal: 1));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 1, Value: "blocked" });
    }

    [Fact]
    public void ABlockedStoryStaysWhileABlockerIsStillBeingBuilt()
    {
        var plan = Make(new Fixture().Story(1, "Backlog", "blocked", blockers: [2]).Story(2, "In-Progress"));

        Assert.DoesNotContain(plan.Chores, c => c.Story == 1);
    }

    [Fact]
    public void ABlockedLabelWithNoBlockerLinkedIsAHandsAndStays()
    {
        var plan = Make(new Fixture().Story(1, "Backlog", "blocked"));

        Assert.DoesNotContain(plan.Chores, c => c.Story == 1);
    }

    [Fact]
    public void AnAnalyzingLockWithNoAnalysisBehindItComesOff()
    {
        var plan = Make(new Fixture().Story(1, "Analyze", "analyzing").LockAge(1, "analyzing", TimeSpan.FromMinutes(30)));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 1, Value: "analyzing" });
        Assert.Contains(plan.Items, i => i.Kind == WorkKind.Analyze && i.Story == 1);
    }

    [Fact]
    public void AClaimAnotherHarnessHoldsIsNeverStale()
    {
        var plan = Make(new Fixture().Story(1, "In-Progress", "claimed").LockAge(1, "claimed", TimeSpan.FromHours(2)).HeldBy(1, "box2"));

        Assert.Empty(plan.Chores);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void TheGatesVerdictIsTheLastMarkAtThatHead()
    {
        var bodies = new[]
        {
            "**Gate** rojo <!-- harness:gate:red:aaa -->",
            "**Gate** verde <!-- harness:gate:green:aaa -->",
            "**Gate** rojo <!-- harness:gate:red:bbb -->",
        };

        Assert.Equal(CiState.Green, Markers.GateAt(bodies, "aaa"));
        Assert.Equal(CiState.Red, Markers.GateAt(bodies, "bbb"));
        Assert.Null(Markers.GateAt(bodies, "ccc"));
        Assert.Null(Markers.GateAt([.. bodies, "anulado <!-- harness:gate:void:aaa -->"], "aaa"));
    }

    [Theory]
    [InlineData("red", 1)]
    [InlineData("round", 2)]
    [InlineData("review", 3)]
    [InlineData("gate", 3)]
    [InlineData("rest", 9)]
    public void TheMoreUrgentTheTierTheLowerItsRank(string tier, int rank)
    {
        Assert.Equal(rank, Planner.Rank(tier));
    }
}
