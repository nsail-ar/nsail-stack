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
        var board = new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending).Story(2, "In-Progress").Pr(2, 20, gate: CiState.Pending)
            .Story(3).Story(4, "Ready", "p1").Build();

        var plan = SampleWorkers.Plan(board, new PlannerOptions(Gate: true) { WipLimit = 2 });

        Assert.DoesNotContain(plan.Items, i => i.Story is 3 or 4);
    }

    [Theory]
    [InlineData("CI red on main: Foo", "")]
    [InlineData("CI red on main: Foo", "p1")]
    [InlineData("Regression red on main (Shop)", "")]
    [InlineData("Regression red on main (Shop)", "p1")]
    public void TheWipLimitDoesNotHoldARedStory(string title, string labels)
    {
        var full = new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending).Story(2, "In-Progress").Pr(2, 20, gate: CiState.Pending)
            .Story(3, "Ready", labels, title: title).Build();
        // A red story under way counts like any other: the plain story beside it is held.
        var started = new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending).Story(3, "In-Progress", labels, title: title).Pr(3, 30, gate: CiState.Pending)
            .Story(4).Build();
        var options = new PlannerOptions(Gate: true) { WipLimit = 2 };

        Assert.Contains("dev #3 (red)", SampleWorkers.Plan(full, options).Items.Select(i => i.ToString()));
        Assert.DoesNotContain(SampleWorkers.Plan(started, options).Items, i => i.Story == 4);
    }

    [Fact]
    public void TheLimitsNoteNamesTheStoryItHolds()
    {
        var f = new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending).Story(2, "In-Progress").Pr(2, 20, gate: CiState.Pending);
        var options = new PlannerOptions(Gate: true) { WipLimit = 2 };
        const string limit = "2 stories under way (limit 2): no new story starts until one closes";

        var held = SampleWorkers.Plan(f.Story(5).Story(4, "Ready", "p1").Build(), options);
        var none = SampleWorkers.Plan(new Fixture().Story(1, "In-Progress").Pr(1, 10, gate: CiState.Pending).Story(2, "In-Progress").Pr(2, 20, gate: CiState.Pending)
            .Story(3, "Ready", "bug").Build(), options);

        Assert.Contains($"{limit} — #4 would be next", held.Notes);
        Assert.Contains(limit, none.Notes);
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
