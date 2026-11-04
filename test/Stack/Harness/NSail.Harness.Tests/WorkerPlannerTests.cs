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
        // A red story is born p1 (RedLabels): it leads the p1 sweep, ahead of the other p1 stories.
        var f = new Fixture()
            .Story(1, "Ready", "p1")
            .Story(2, "In-Progress").Pr(2, 20)
            .Story(3, "Ready", "p1", title: "Regression red on main (Shop)");

        Assert.Equal(["dev #3 (red)", "dev #1 (p1)", "review PR #20 (story #2)"], Lines(f));
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

    [Fact]
    public void WorkedOrWithAPrOrAnEpicIsNoOrphan()
    {
        Assert.Empty(Plan(new Fixture().Story(1, "In-Progress").Flying(dev: [1])).Chores);
        Assert.DoesNotContain(Plan(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Pending)).Chores, c => c.Story == 1);
        Assert.DoesNotContain(Plan(new Fixture().Story(1, "In-Progress", subs: 2)).Chores, c => c.Story == 1);
    }
}
