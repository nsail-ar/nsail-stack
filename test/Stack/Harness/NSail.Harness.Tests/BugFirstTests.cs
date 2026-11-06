// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

// A `bug` is broken where a customer runs: it goes before everything, a release and finishing work included.
public class BugFirstTests
{
    [Fact]
    public void ABugGoesBeforeADueCutAndBeforeP1()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(1, "Ready", "p1").Story(2, "Ready", "bug").Due("release-shop.yml").Build());

        Assert.Equal(2, plan.Items[0].Story);
        Assert.Equal("bug", plan.Items[0].Tier);
        Assert.Equal(WorkKind.Release, plan.Items[1].Kind);
    }

    [Fact]
    public void ABugsPrIsReadBeforeAnotherStorysPr()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(1, "In-Progress").Pr(1, 10).Story(2, "In-Progress", "bug").Pr(2, 20).Build());

        Assert.Equal(20, plan.Items[0].Pr);
        Assert.Equal("bug", plan.Items[0].Tier);
    }

    [Fact]
    public void TheWipLimitDoesNotHoldABug()
    {
        var board = new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending).Story(2, "In-Progress").Pr(2, 20, gate: CiState.Pending)
            .Story(3, "Ready", "bug").Build();

        var plan = SampleWorkers.Plan(board, new PlannerOptions(Gate: true) { WipLimit = 2 });

        Assert.Contains(plan.Items, i => i.Story == 3 && i.Kind == WorkKind.Dev);
    }

    [Fact]
    public void ABugIsTheMostUrgentTurn()
    {
        Assert.True(Planner.Rank("bug") < Planner.Rank("release"));
    }
}
