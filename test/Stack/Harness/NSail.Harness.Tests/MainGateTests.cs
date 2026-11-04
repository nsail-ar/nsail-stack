// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// main's head is judged whenever it moves past what the gate last judged, before anything else.
public class MainGateTests
{
    [Fact]
    public void AMovedMainIsGatedFirst()
    {
        var board = new Fixture().Story(1, "Ready", "p1").Build() with { MainSha = "bbb", MainGated = "aaa" };

        var plan = SampleWorkers.Plan(board, new PlannerOptions(Gate: true));

        Assert.Equal(WorkKind.Main, plan.Items[0].Kind);
        Assert.Equal("bbb", plan.Items[0].Sha);
    }

    [Fact]
    public void AJudgedHeadIsNotGatedAgain()
    {
        var board = new Fixture().Build() with { MainSha = "aaa", MainGated = "aaa" };

        Assert.DoesNotContain(SampleWorkers.Plan(board, new PlannerOptions(Gate: true)).Items, i => i.Kind == WorkKind.Main);
    }

    [Fact]
    public void AGateOfMainAlreadyRunningIsNotSentTwice()
    {
        var board = new Fixture().Build() with { MainSha = "bbb", MainGated = "aaa" };
        board = board with { Flight = board.Flight with { MainGating = true } };

        Assert.DoesNotContain(SampleWorkers.Plan(board, new PlannerOptions(Gate: true)).Items, i => i.Kind == WorkKind.Main);
    }
}
