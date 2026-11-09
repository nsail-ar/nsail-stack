// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

public class PlannerTests
{
    static List<string> Lines(Fixture f)
    {
        return SampleWorkers.Plan(f.Build()).Items.Select(i => i.ToString()).ToList();
    }

    [Fact]
    public void TheOrderIsFinishingBeforeStarting()
    {
        var f = new Fixture()
            .Story(10, "Ready")                                  // rest
            .Story(11, "Ready", "p1")                            // p1
            .Story(12, "Analyze")                                // analyze
            .Story(13, "Analyze", "stalled")                     // stalled
            .Story(14, "In-Progress").Pr(14, 140)                // review (green, unread)
            .Story(15, "In-Progress").Pr(15, 150, CiState.Red)   // round
            .Story(16, "Merged", title: "CI red on main: X (Shop)").Release(16)
            .Story(17, "Ready", parentPercent: 50, parent: 1);   // epic

        // p1 work goes first in every worker: the p1 story is ahead of every review and analysis.
        Assert.Equal(
        [
            "release shop (the red fix #16 merged)",
            "dev #11 (p1)",
            "review PR #140 (story #14)",
            "dev #15 (round (PR #150: CI red))",
            "analyze #13 (stalled)",
            "analyze #12 (analyze)",
            "dev #17 (epic)",
            "dev #10 (rest)",
        ], Lines(f));
    }

    [Fact]
    public void APinPrOfAMergedStoryIsReviewed()
    {
        // A Stack story is Merged once its framework PR lands; its pin PR here is still open.
        var f = new Fixture().Story(2246, "Merged").Pr(2246, 2669);

        Assert.Equal(["review PR #2669 (story #2246)"], Lines(f));
    }

    [Fact]
    public void AStalledStoryInReadyGoesToAnalyzeForTheAnalyst()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(2673, "Ready", "stalled").Pr(2673, 2815, CiState.Red).Build());

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 2673, Value: "Analyze" });
        Assert.Equal(["analyze #2673 (stalled)"], plan.Items.Select(i => i.ToString()));
    }

    [Fact]
    public void AStalledStoryUnderALockWaitsForIt()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(5, "Ready", "stalled,claimed").Flying(dev: [5]).Build());

        Assert.Empty(plan.Items);
        Assert.Empty(plan.Chores);
    }

    [Fact]
    public void AStalledStoryOutsideTheStoryStagesIsAHandsAndSaidOutLoud()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(5, "Released", "stalled").Build());

        Assert.Empty(plan.Items);
        Assert.Contains(plan.Notes, n => n.Contains("#5"));
    }

    [Fact]
    public void AStoryInQaWithNoAskIsSaidOutLoud()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(6, "QA").Story(7, "QA", "qa-asked").Build());

        Assert.Contains(plan.Notes, n => n.StartsWith("#6: in QA with no ask", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Notes, n => n.StartsWith("#7:", StringComparison.Ordinal));
    }

    [Fact]
    public void AStackPrGreenAndUnreadPastItsReviewsLimitIsSaidOutLoud()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(8, "In-Progress").Pr(8, 174, repo: Fixture.Stack, greenFor: TimeSpan.FromMinutes(61)).Build());

        Assert.Contains(plan.Notes, n => n.StartsWith("#8: framework PR #174 green and unread", StringComparison.Ordinal));
    }

    [Fact]
    public void AStackPrGreenForLessThanItsReviewsLimitIsNotSaid()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(8, "In-Progress").Pr(8, 174, repo: Fixture.Stack, greenFor: TimeSpan.FromMinutes(59)).Build());

        Assert.DoesNotContain(plan.Notes, n => n.StartsWith("#8:", StringComparison.Ordinal));
    }

    [Fact]
    public void AStackPrOfAStoryAHandHoldsIsNotSaid()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(8, "In-Progress", "hand").Pr(8, 142, repo: Fixture.Stack, greenFor: TimeSpan.FromMinutes(61)).Build());

        Assert.DoesNotContain(plan.Notes, n => n.StartsWith("#8:", StringComparison.Ordinal));
    }

    [Fact]
    public void AHomePrGreenAndUnreadIsReviewedHereNotSaid()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(8, "In-Progress").Pr(8, 140, greenFor: TimeSpan.FromMinutes(61)).Build());

        Assert.Equal(["review PR #140 (story #8)"], plan.Items.Select(i => i.ToString()));
        Assert.DoesNotContain(plan.Notes, n => n.StartsWith("#8:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ruling")]
    [InlineData("blocked")]
    [InlineData("hand")]
    [InlineData("claimed")]
    [InlineData("review")]
    [InlineData("analyzing")]
    [InlineData("walking")]
    public void AHeldStoryIsNotTaken(string hold)
    {
        Assert.Empty(Lines(new Fixture().Story(1, "Ready", hold).LockAge(1, hold, TimeSpan.FromMinutes(1))));
    }

    [Fact]
    public void AStoryForADriverNoHarnessHereRunsIsHeld()
    {
        Assert.Empty(SampleWorkers.Plan(new Fixture().Story(1, "Ready", "codex:gpt-5").Build(), new PlannerOptions { UnrunnableDrivers = ["codex"] }).Items);
        Assert.Single(Lines(new Fixture().Story(1, "Ready", "codex:gpt-5")));
    }

    [Fact]
    public void AStaleClaimComesOffAndTheStoryIsTakenAgain()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(7, "Ready", "claimed").LockAge(7, "claimed", TimeSpan.FromMinutes(11)).Build());

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 7, Value: "claimed" });
        Assert.Equal(["dev #7 (rest)"], plan.Items.Select(i => i.ToString()));
    }

    [Fact]
    public void AllowProtectedLiftsTheScreensRulingAndTheStoryIsTaken()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(7, "Ready", "ruling,screened,allow-protected").Build());

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 7, Value: "ruling" });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 7, Value: "screened" });
        Assert.Equal(["dev #7 (rest)"], plan.Items.Select(i => i.ToString()));
    }

    [Fact]
    public void AllowProtectedLeavesARulingTheScreenDidNotSet()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(7, "Ready", "ruling,allow-protected").Build());

        Assert.Empty(plan.Chores);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void TheScreensRulingWaitsForAllowProtected()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(7, "Ready", "ruling,screened").Build());

        Assert.Empty(plan.Chores);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void AScreenedMarkWithItsRulingLiftedComesOff()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(7, "Ready", "screened").Build());

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 7, Value: "screened" });
        Assert.Equal(["dev #7 (rest)"], plan.Items.Select(i => i.ToString()));
    }

    [Fact]
    public void AFreshClaimStays()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(7, "Ready", "claimed").LockAge(7, "claimed", TimeSpan.FromMinutes(5)).Build());

        Assert.Empty(plan.Chores);
        Assert.Empty(plan.Items);
    }


    [Fact]
    public void AStaleReviewLockComesOffWhenNoReviewRunsOnItsPrs()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(8, "In-Progress", "review").Pr(8, 80).LockAge(8, "review", TimeSpan.FromMinutes(20)).Build());

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 8, Value: "review" });
        Assert.Equal(["review PR #80 (story #8)"], plan.Items.Select(i => i.ToString()));
    }

    [Fact]
    public void APendingPrWaitsAndHoldsItsStory()
    {
        Assert.Empty(Lines(new Fixture().Story(3, "In-Progress").Pr(3, 30, CiState.Pending)));
    }

    [Fact]
    public void AReadPrGoesToARoundWithTheWordItGot()
    {
        Assert.Equal(["dev #3 (round (PR #30 read findings at abcdef12))"], Lines(new Fixture().Story(3, "In-Progress").Pr(3, 30, word: "findings")));
    }

    [Fact]
    public void AConflictingPrGoesToARound()
    {
        Assert.Equal(["dev #3 (round (PR #30: CI conflict))"], Lines(new Fixture().Story(3, "In-Progress").Pr(3, 30, CiState.Conflict)));
    }

    [Fact]
    public void AGreenStackPrIsLeftToTheStacksOwnReview()
    {
        Assert.Empty(Lines(new Fixture().Story(3, "In-Progress").Pr(3, 130, repo: Fixture.Stack)));
    }

    [Fact]
    public void AStackPrReadWithFindingsGoesToARoundHere()
    {
        Assert.Equal(["dev #3 (round (framework PR #130 read findings at abcdef12))"],
            Lines(new Fixture().Story(3, "In-Progress").Pr(3, 130, word: "findings", repo: Fixture.Stack)));
    }

    [Fact]
    public void ReviewsGoBeforeRoundsInsideTheirTier()
    {
        var f = new Fixture()
            .Story(1, "In-Progress").Pr(1, 10, CiState.Red)
            .Story(2, "In-Progress").Pr(2, 20);

        Assert.Equal(["review PR #20 (story #2)", "dev #1 (round (PR #10: CI red))"], Lines(f));
    }

    [Fact]
    public void NothingRunsTwice()
    {
        var f = new Fixture()
            .Story(1, "In-Progress").Pr(1, 10).Flying(reviewPrs: [10])
            .Story(2, "Ready")
            .Story(3, "Analyze");
        var f2 = new Fixture().Story(2, "Ready").Story(3, "Analyze").Flying(dev: [2], analyze: [3]);

        Assert.Equal(["analyze #3 (analyze)", "dev #2 (rest)"], Lines(f));
        Assert.Empty(Lines(f2));
    }

    [Fact]
    public void AnAnalyzeStoryWithAnOpenPrIsTheRoundsNotAnalysts()
    {
        Assert.Equal(["review PR #40 (story #4)"], Lines(new Fixture().Story(4, "Analyze").Pr(4, 40)));
    }

    [Fact]
    public void AnEpicIsNeverBuiltButItsCardMovesWhenAChildStarts()
    {
        var plan = SampleWorkers.Plan(new Fixture()
            .Story(100, "Ready", subs: 3)
            .Story(101, "In-Progress", parent: 100, parentPercent: 0)
            .Flying(dev: [101])
            .Build());

        Assert.Empty(plan.Items);
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 100, Value: "In-Progress" });
    }

    [Fact]
    public void ABlockedStoryWaitsUntilItsBlockerLands()
    {
        Assert.Empty(Lines(new Fixture().Story(1, "Ready", blockers: [2]).Story(2, "In-Progress").Flying(dev: [2])));
        Assert.Equal(["dev #1 (rest)"], Lines(new Fixture().Story(1, "Ready", blockers: [2]).Story(2, "Merged")));
    }

    [Fact]
    public void EpicsClosestToDoneGoFirst()
    {
        var f = new Fixture()
            .Story(1, "Ready", parentPercent: 20, parent: 90)
            .Story(2, "Ready", parentPercent: 80, parent: 91)
            .Story(3, "Ready", parentPercent: 80, parent: 91);

        Assert.Equal(["dev #2 (epic)", "dev #3 (epic)", "dev #1 (epic)"], Lines(f));
    }

    [Fact]
    public void AStoryInAnAreaNoLaneBuildsGoesFirst()
    {
        var f = new Fixture()
            .Story(1, "Ready", "area:shop")
            .Story(2, "Ready", "area:clinic")
            .Story(9, "In-Progress", "area:shop").Flying(dev: [9]);

        Assert.Equal(["dev #2 (rest)", "dev #1 (rest)"], Lines(f));
    }

    [Fact]
    public void TheRedStoryJumpsEveryQueue()
    {
        var f = new Fixture()
            .Story(1, "Ready", "p1")
            .Story(2, "In-Progress").Pr(2, 20)
            .Story(3, "Ready", title: "Regression red on main (Shop)");

        Assert.Equal(["dev #3 (red)", "dev #1 (p1)", "review PR #20 (story #2)"], Lines(f));
    }

    [Fact]
    public void TheRedStorysPrIsReadBeforeAnyOther()
    {
        var f = new Fixture()
            .Story(2, "In-Progress").Pr(2, 20)
            .Story(3, "In-Progress", title: "CI red on main: Foo").Pr(3, 30);

        Assert.Equal(["review PR #30 (story #3)", "review PR #20 (story #2)"], Lines(f));
    }

    [Fact]
    public void AStoryIsPlannedOnceEvenWhenTwoTiersWantIt()
    {
        // A stalled story in Analyze is the stalled tier's, never the analyze tier's as well.
        Assert.Equal(["analyze #5 (stalled)"], Lines(new Fixture().Story(5, "Analyze", "stalled")));
    }
}
