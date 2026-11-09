// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

// The workers engine against the planner it replaces, on the planner's own boards: NSail's workers
// (the host's appsettings.json) must plan what the planner plans, and where they plan otherwise the
// test says so and why (the p1 sweep, a story's one job per pass).
public class WorkerPlannerTests
{
    static readonly IReadOnlyList<Worker> Workers = SampleWorkers.Workers;

    static Plan Plan(Fixture f, PlannerOptions? options = null)
    {
        return WorkerPlanner.Make(f.Build(), Workers, options ?? new PlannerOptions());
    }

    static List<string> Lines(Fixture f, PlannerOptions? options = null)
    {
        return Plan(f, options).Items.Select(i => i.ToString()).ToList();
    }

    [Fact]
    public void NSailsWorkersCompileInPriorityOrder()
    {
        Assert.Equal(["release-shop", "release-clinic", "release-admin", "verify-main", "verify-pr", "review", "round", "test", "test-asked", "analyze-stalled", "analyze", "develop",
            "no-column", "unblock-backlog", "unblock", "epics-start", "orphans-new", "orphans-built"], Workers.Select(w => w.Name));
    }

    [Fact]
    public void P1WorkGoesFirstInEveryWorkerNotOnlyAmongNewStories()
    {
        // The planner put a p1 story behind every review and analysis; the workers sweep p1 first.
        var f = new Fixture()
            .Story(1, "Ready", "p1")
            .Story(2, "In-Progress").Pr(2, 20)
            .Story(3, "Analyze");

        Assert.Equal(["dev #1 (p1)", "review PR #20 (story #2)", "analyze #3 (analyze)"], Lines(f));
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
        Assert.Empty(Lines(new Fixture().Story(1, "Ready", "codex:gpt-5"), new PlannerOptions { UnrunnableDrivers = ["codex"] }));
        Assert.Single(Lines(new Fixture().Story(1, "Ready", "codex:gpt-5")));
    }

    [Fact]
    public void AStalledStoryInReadyGoesToAnalyzeForTheAnalyst()
    {
        var plan = Plan(new Fixture().Story(2673, "Ready", "stalled").Pr(2673, 2815, CiState.Red));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 2673, Value: "Analyze" });
        Assert.Equal(["analyze #2673 (stalled)"], plan.Items.Select(i => i.ToString()));
    }

    [Fact]
    public void TheRedStoryJumpsEveryQueue()
    {
        // A red suite story lights the andon: it is the only story planned.
        var f = new Fixture()
            .Story(1, "Ready", "p1")
            .Story(2, "In-Progress").Pr(2, 20)
            .Story(3, "Ready", "p1", title: "Regression red on main (Shop)");

        Assert.Equal(["dev #3 (red)"], Lines(f));
    }

    [Fact]
    public void AFixedRedStoryWaitsWhileNoGateJudgedAHeadThatHoldsItsMerge()
    {
        var f = new Fixture()
            .Story(1, "Ready", "p1")
            .Story(3, "Ready", "p1", title: "CI red on main: Foo");

        var plan = WorkerPlanner.Make(f.Build() with { UnjudgedFixes = new HashSet<int> { 3 } }, Workers, new PlannerOptions());

        Assert.Equal(["dev #1 (p1)"], plan.Items.Select(i => i.ToString()));
        Assert.Contains(plan.Notes, n => n.StartsWith("#3:", StringComparison.Ordinal));
    }

    // The gate's verdicts along main, as IRepository.Statuses reads them: a running gate's pending mark is none.
    static async Task<IReadOnlySet<int>> Unjudged(MainHistory main, Merge? merge, params StatusMark[] marks)
    {
        var options = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        var repo = new DevJobRunTests.FileRepository(main.Dir) { Landed = merge };
        var reader = new SnapshotReader(new MemoryBoard(), new RepositorySet([(repo, options.Repositories[0])]), null!, options);
        var issues = new Fixture().Story(3, "Ready", "p1", title: "CI red on main: Foo").Build().Issues;
        return await reader.ReadUnjudged(issues, marks);
    }

    [Fact]
    public async Task AFixedRedStoryIsHeldUntilAHeadThatHoldsItsMergeGoesRed()
    {
        using var main = await MainHistory.Create();
        var at = DateTimeOffset.UtcNow;

        // No verdict at all, or only one at the head the fix merged on top of.
        Assert.Equal([3], await Unjudged(main, main.Merge));
        Assert.Equal([3], await Unjudged(main, main.Merge, new StatusMark(main.Parent, Markers.GateContext, false, at)));
        // The head that holds the fix went red: Develop takes it, first.
        Assert.Empty(await Unjudged(main, main.Merge, new StatusMark(main.Fix, Markers.GateContext, false, at), new StatusMark(main.Parent, Markers.GateContext, false, at)));
        // With no merge there is no fix to wait for.
        Assert.Empty(await Unjudged(main, null));
    }

    [Fact]
    public void TheRedStorysPrIsReadBeforeAnyOther()
    {
        var f = new Fixture()
            .Story(2, "In-Progress").Pr(2, 20)
            .Story(3, "In-Progress", "p1", title: "CI red on main: Foo").Pr(3, 30);

        Assert.Equal(["review PR #30 (story #3)", "review PR #20 (story #2)"], Lines(f));
    }

    [Fact]
    public void AReleasedStoryInItsVersionIsWalkedOnItsOwn()
    {
        Assert.Equal(["qa #1"], Lines(new Fixture().Story(1, "Released", milestone: true)));
        Assert.Empty(Lines(new Fixture().Story(1, "Released")));
        Assert.Empty(Lines(new Fixture().Story(1, "Released", "walking", milestone: true).LockAge(1, "walking", TimeSpan.FromMinutes(1))));
        Assert.Equal(["qa #2"], Lines(new Fixture().Story(2, "QA", "qa-asked", milestone: true)));
        Assert.Empty(Lines(new Fixture().Story(2, "QA", milestone: true)));
    }

    [Fact]
    public void TheBoardKeepersDoThePlannersChores()
    {
        var plan = Plan(new Fixture()
            .Story(1, "-")
            .Story(2, "Backlog", "blocked", blockers: [9]).Story(9, "Merged")
            .Story(3, "Ready", "blocked", blockers: [9])
            .Story(4, "Ready", "blocked")
            .Story(100, "Ready", subs: 3).Story(101, "In-Progress", parent: 100).Flying(dev: [101]));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 1, Value: "Backlog" });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 2, Value: "blocked" });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 2, Value: "Analyze" });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 3, Value: "blocked" });
        Assert.DoesNotContain(plan.Chores, c => c.Story == 3 && c.Kind == ChoreKind.MoveStage);
        // A `blocked` with no blocker linked is a hand's, and stays.
        Assert.DoesNotContain(plan.Chores, c => c.Story == 4);
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 100, Value: "In-Progress" });
    }

    [Fact]
    public void AWorkerOffByWhenPlansNothing()
    {
        var f = new Fixture().Story(1, "Ready").Story(2, "Analyze");

        var plan = WorkerPlanner.Make(f.Build(), Workers, new PlannerOptions(), w => w.Name != "develop");

        Assert.Equal(["analyze #2 (analyze)"], plan.Items.Select(i => i.ToString()));
    }

    [Fact]
    public void AStoryGetsOneJobAPass()
    {
        var items = Plan(new Fixture().Story(5, "Analyze", "stalled")).Items;

        Assert.Single(items);
    }

    [Fact]
    public void WipLimitHoldsOnlyNewStories()
    {
        var f = new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Red).Story(2, "Ready");

        Assert.Equal(["dev #1 (round (PR #10: CI red))"], Lines(f, new PlannerOptions { WipLimit = 1 }));
    }

    [Fact]
    public void ARedPrStillGetsItsRoundBesideAGreenOneElsewhere()
    {
        var f = new Fixture()
            .Story(5, "In-Progress")
            .Pr(5, 50, repo: "acme/other").Pr(5, 51, CiState.Red);

        Assert.Contains(Lines(f), l => l.Contains("PR #51"));
    }

    [Fact]
    public void StartingChoresRunOnlyForItemsThatStart()
    {
        var plan = Plan(new Fixture().Story(2673, "Ready", "stalled"));
        var started = HarnessLoop.ChoresToDo(plan, plan.Items);
        var paused = HarnessLoop.ChoresToDo(plan, []);

        Assert.Contains(started, c => c is { Kind: ChoreKind.MoveStage, Story: 2673, Value: "Analyze" });
        Assert.DoesNotContain(paused, c => c.Story == 2673);
    }

    [Fact]
    public void AnEarlierQuotaEndKeepsTheLaterWall()
    {
        var week = DateTimeOffset.UtcNow.AddDays(7);
        var hours = DateTimeOffset.UtcNow.AddHours(5);

        Assert.Equal(week, HarnessLoop.LaterWall(week, hours));
        Assert.Equal(week, HarnessLoop.LaterWall(hours, week));
        Assert.Equal(hours, HarnessLoop.LaterWall(null, hours));
    }

    [Fact]
    public void AForcedJobRefusesInDebugWhilePausedAndInsideTheQuotaWall()
    {
        var wall = DateTimeOffset.UtcNow.AddHours(1);

        Assert.Null(HarnessLoop.ForceRefusal(HarnessMode.Live, null, null, WorkKind.Dev));
        Assert.Contains("Debug", HarnessLoop.ForceRefusal(HarnessMode.Debug, null, null, WorkKind.Dev));
        Assert.Contains("paused", HarnessLoop.ForceRefusal(HarnessMode.Live, "drain", null, WorkKind.Dev));
        Assert.Contains("quota", HarnessLoop.ForceRefusal(HarnessMode.Live, null, wall, WorkKind.Dev));
        Assert.Null(HarnessLoop.ForceRefusal(HarnessMode.Live, null, wall, WorkKind.Release));
        var now = new DateTimeOffset(2026, 11, 9, 14, 0, 0, TimeSpan.Zero);
        Assert.Equal("17:15", HarnessLoop.WallClock(now.AddHours(3).AddMinutes(15), now));
        Assert.Equal("Mon 14:00", HarnessLoop.WallClock(now.AddDays(7), now));
    }
}

public class OrphanTests
{
    static readonly IReadOnlyList<Worker> Workers = SampleWorkers.Workers;

    static Plan Plan(Fixture f)
    {
        return WorkerPlanner.Make(f.Build(), Workers, new PlannerOptions());
    }

    [Fact]
    public void AnOrphanNeverBuiltGoesBackToReady()
    {
        var plan = Plan(new Fixture().Story(1, "In-Progress"));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 1, Value: "Ready" });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.Comment, Story: 1 });
    }

    [Fact]
    public void AnOrphanWithAMergedPrGoesToTheAnalyst()
    {
        var plan = Plan(new Fixture().Story(1, "In-Progress").MergedPr(1));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 1, Value: "Analyze" });
        Assert.DoesNotContain(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Value: "Ready" });
    }

    [Theory]
    [InlineData("hand")]
    [InlineData("blocked")]
    [InlineData("ruling")]
    [InlineData("claimed")]
    public void ANotOrphanIsLeftAlone(string label)
    {
        Assert.Empty(Plan(new Fixture().Story(1, "In-Progress", label).LockAge(1, label, TimeSpan.FromMinutes(1))).Chores);
    }

    // A job's ending takes its label off before its PR opens; the fleet's lock and the grace span it.
    [Fact]
    public void AStoryAnotherHarnessHoldsIsNoOrphan()
    {
        Assert.DoesNotContain(Plan(new Fixture().Story(1, "In-Progress", quiet: TimeSpan.FromSeconds(10)).HeldBy(1, "box2")).Chores, c => c.Story == 1);
        Assert.DoesNotContain(Plan(new Fixture().Story(1, "In-Progress", quiet: TimeSpan.FromHours(1)).HeldBy(1, "box2")).Chores, c => c.Story == 1);
    }

    [Fact]
    public void AStoryTouchedWithinTheGraceIsNoOrphan()
    {
        Assert.DoesNotContain(Plan(new Fixture().Story(1, "In-Progress", quiet: TimeSpan.FromSeconds(10))).Chores, c => c.Story == 1);
        Assert.DoesNotContain(Plan(new Fixture().Story(1, "In-Progress", quiet: TimeSpan.FromMinutes(9)).MergedPr(1)).Chores, c => c.Story == 1);
    }

    [Fact]
    public void AStoryQuietPastTheGraceIsAnOrphan()
    {
        var plan = Plan(new Fixture().Story(1, "In-Progress", quiet: TimeSpan.FromMinutes(17)).Story(2, "In-Progress", quiet: TimeSpan.FromMinutes(11)).MergedPr(2));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 1, Value: "Ready", NoPr: true });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 2, Value: "Analyze", NoPr: true });
    }

    [Fact]
    public void TheGraceIsTheOptions()
    {
        var f = new Fixture().Story(1, "In-Progress", quiet: TimeSpan.FromMinutes(3));

        Assert.Contains(WorkerPlanner.Make(f.Build(), Workers, new PlannerOptions { OrphanGrace = TimeSpan.FromMinutes(2) }).Chores, c => c.Story == 1);
        Assert.Equal(TimeSpan.FromMinutes(10), HarnessLoop.PlannerOptionsOf(new HarnessOptions()).OrphanGrace);
        Assert.Equal(TimeSpan.FromMinutes(4), HarnessLoop.PlannerOptionsOf(new HarnessOptions { OrphanGraceMinutes = 4 }).OrphanGrace);
    }

    // Only a keeper that reads an absent PR waits: the others move the story on the pass that sees it.
    [Fact]
    public void EveryOtherKeeperMovesWithNoGrace()
    {
        var plan = Plan(new Fixture()
            .Story(1, "-", quiet: TimeSpan.FromSeconds(5)).HeldBy(1, "box2")
            .Story(2, "Backlog", "blocked", blockers: [9], quiet: TimeSpan.FromSeconds(5)).Story(9, "Merged")
            .Story(100, "Ready", subs: 3, quiet: TimeSpan.FromSeconds(5)).Story(101, "In-Progress", parent: 100).Flying(dev: [101]));

        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 1, Value: "Backlog", NoPr: false });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 2, Value: "Analyze" });
        Assert.Contains(plan.Chores, c => c is { Kind: ChoreKind.MoveStage, Story: 100, Value: "In-Progress" });
    }

    [Fact]
    public void WorkedOrWithAPrOrAnEpicIsNoOrphan()
    {
        Assert.Empty(Plan(new Fixture().Story(1, "In-Progress").Flying(dev: [1])).Chores);
        Assert.DoesNotContain(Plan(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Pending)).Chores, c => c.Story == 1);
        Assert.DoesNotContain(Plan(new Fixture().Story(1, "In-Progress", subs: 2)).Chores, c => c.Story == 1);
    }
}
