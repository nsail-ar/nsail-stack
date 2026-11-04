// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// Closing comes before starting: past the limit of stories under way, the queue waits.
public class WipLimitTests
{
    [Fact]
    public void AtTheLimitNoNewStoryStarts()
    {
        var board = new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending).Story(2, "In-Progress").Pr(2, 20, gate: CiState.Pending).Story(3).Build();

        var plan = SampleWorkers.Plan(board, new PlannerOptions(Gate: true) { WipLimit = 2 });

        Assert.DoesNotContain(plan.Items, i => i.Story == 3);
    }

    [Fact]
    public void UnderTheLimitTheQueueMoves()
    {
        var board = new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending).Story(3).Build();

        var plan = SampleWorkers.Plan(board, new PlannerOptions(Gate: true) { WipLimit = 2 });

        Assert.Contains(plan.Items, i => i.Story == 3 && i.Kind == WorkKind.Dev);
    }

    [Fact]
    public void APrWaitingOnAnaDoesNotHoldTheLine()
    {
        var board = new Fixture().Story(1, "In-Progress", "ruling").Pr(1, 10).Story(2, "In-Progress", "ruling").Pr(2, 20).Story(3).Build();

        var plan = SampleWorkers.Plan(board, new PlannerOptions(Gate: true) { WipLimit = 2 });

        Assert.Contains(plan.Items, i => i.Story == 3);
    }

    [Fact]
    public void ThePrsThemselvesAreStillWorkedAtTheLimit()
    {
        var board = new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Red).Story(2, "In-Progress").Pr(2, 20).Story(3).Build();

        var plan = SampleWorkers.Plan(board, new PlannerOptions(Gate: true) { WipLimit = 2 });

        Assert.Contains(plan.Items, i => i.Story == 1 && i.Kind == WorkKind.Dev);
        Assert.DoesNotContain(plan.Items, i => i.Story == 3);
    }
}
