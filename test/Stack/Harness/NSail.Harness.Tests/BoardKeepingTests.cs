// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Repositories;
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
            "**Gate** rojo\n<!-- harness:gate:red:aaa -->",
            "**Gate** verde\n<!-- harness:gate:green:aaa -->",
            "**Gate** rojo\n<!-- harness:gate:red:bbb -->",
        };

        Assert.Equal(CiState.Green, Markers.GateAt(bodies, "aaa"));
        Assert.Equal(CiState.Red, Markers.GateAt(bodies, "bbb"));
        Assert.Null(Markers.GateAt(bodies, "ccc"));
        Assert.Null(Markers.GateAt([.. bodies, "anulado\n<!-- harness:gate:void:aaa -->"], "aaa"));
    }

    [Fact]
    public void AMarkerFromAnotherAuthorOrNotOnItsOwnLineIsNotRead()
    {
        var thread = new[]
        {
            new PrNote(DateTimeOffset.UnixEpoch, "x\n<!-- harness:gate:green:aaa -->", "builder"),
            new PrNote(DateTimeOffset.UnixEpoch, "x\n<!-- harness:gate:red:bbb -->", "crew"),
            new PrNote(DateTimeOffset.UnixEpoch, "see <!-- harness:gate:green:bbb --> here", "crew"),
        };

        var bodies = new Markers("reviewer", ["crew"]).Bodies(thread, "crew").ToList();

        Assert.Null(Markers.GateAt(bodies, "aaa"));
        Assert.Equal(CiState.Red, Markers.GateAt(bodies, "bbb"));
    }

    [Fact]
    public void AMarkerAnyListedHarnessWroteCountsAndAForgedOneDoesNot()
    {
        var markers = new Markers("reviewer", ["crew", "github-actions[bot]", "Ana"]);
        var byAna = new[] { new PrNote(DateTimeOffset.UnixEpoch, "x\n<!-- harness:gate:green:abc123 -->\n<!-- reviewer:findings:abc123 -->", "ana") };
        var byCrew = new[] { new PrNote(DateTimeOffset.UnixEpoch, "x\n<!-- harness:gate:green:abc123 -->", "crew") };
        var byMallory = new[] { new PrNote(DateTimeOffset.UnixEpoch, "x\n<!-- harness:gate:green:abc123 -->\n<!-- reviewer:findings:abc123 -->", "mallory") };

        Assert.Equal(CiState.Green, Markers.GateAt(markers.Bodies(byAna, "crew"), "abc123"));
        Assert.Equal(["abc123"], markers.Findings(markers.Bodies(byAna, "crew")));
        Assert.Equal(CiState.Green, Markers.GateAt(markers.Bodies(byCrew, "ana"), "abc123"));
        Assert.Null(Markers.GateAt(markers.Bodies(byMallory, "crew"), "abc123"));
        Assert.Null(Markers.GateAt(markers.Bodies(byMallory, "ana"), "abc123"));
        Assert.Empty(markers.Findings(markers.Bodies(byMallory, "crew")));
    }

    [Fact]
    public void WithNoListOnlyTheReadersOwnMarkersCount()
    {
        var markers = new Markers("reviewer");
        var thread = new[]
        {
            new PrNote(DateTimeOffset.UnixEpoch, "x\n<!-- harness:gate:green:aaa -->", "CREW"),
            new PrNote(DateTimeOffset.UnixEpoch, "x\n<!-- harness:gate:green:bbb -->", "ana"),
        };

        Assert.Equal(CiState.Green, Markers.GateAt(markers.Bodies(thread, "crew"), "aaa"));
        Assert.Null(Markers.GateAt(markers.Bodies(thread, "crew"), "bbb"));
        Assert.Equal(CiState.Green, Markers.GateAt(new Markers("reviewer", ["ana"]).Bodies(thread, "crew"), "aaa"));
    }

    [Fact]
    public void AMarkerBuiltByRemovingAnotherIsRemovedToo()
    {
        var markers = new Markers("reviewer");

        Assert.Equal("\n", markers.Strip("<!-- har<!-- harness:gate:void:aaa -->ness:gate:green:abc -->\n"));
    }

    [Fact]
    public void TheBuildersWordsLoseTheirMarkers()
    {
        var markers = new Markers("reviewer");

        Assert.Equal("ok \n\n", markers.Strip("ok <!-- harness:gate:green:aaa -->\n<!-- reviewer:clean:aaa -->\n"));
    }

    [Fact]
    public void AReviewMarkerInsideALineIsNotRead()
    {
        var markers = new Markers("reviewer");

        Assert.Null(markers.ReviewAt(["see <!-- reviewer:clean:aaa --> here"], "aaa"));
        Assert.Equal("clean", markers.ReviewAt(["see\n<!-- reviewer:clean:aaa -->"], "aaa"));
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
