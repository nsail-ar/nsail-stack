// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

public class MarksTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    static StoryComment Said(int minute, string body, string author = "harness")
    {
        return new StoryComment(T0.AddMinutes(minute), body, author);
    }

    static readonly Markers Own = new("review");

    [Fact]
    public void AnotherHarnessesPendingGateMarkKeepsTheHeadFromASecondGateWhileItCanStillBeRunning()
    {
        var now = DateTimeOffset.UtcNow;
        var hold = HarnessLoop.PendingGate(100);

        Assert.Equal("c1", HarnessLoop.MainGatedOf([new StatusMark("c1", Markers.GateContext, false, now.AddMinutes(-200), Pending: true)], now, hold));
        Assert.Null(HarnessLoop.MainGatedOf([new StatusMark("c1", Markers.GateContext, false, now.AddHours(-5), Pending: true)], now, hold));
        Assert.Null(StatusMark.Newest([new StatusMark("c1", Markers.GateContext, false, now, Pending: true)], Markers.GateContext));
    }

    [Fact]
    public void ThePendingHoldIsTheLongestMainWorkersCutPlusAnHour()
    {
        var longest = SampleWorkers.Workers.Where(w => w.Kind == WorkKind.Main).Max(w => w.Options.Minutes ?? w.Type.Minutes);

        Assert.Equal(TimeSpan.FromMinutes(longest * 2 + 60), HarnessLoop.MainPendingFor(SampleWorkers.Workers));
        Assert.Equal(TimeSpan.Zero, HarnessLoop.MainPendingFor(SampleWorkers.Workers.Where(w => w.Kind != WorkKind.Main).ToList()));
    }

    [Fact]
    public void BreaksCountOnlyWhileNothingElseIsSaidBetweenThem()
    {
        var comments = new[]
        {
            Said(1, $"broke\n{Markers.Broke}"),
            Said(2, "a person looked"),
            Said(3, $"broke\n{Markers.Broke}"),
            Said(4, $"broke\n{Markers.Broke}"),
        };

        Assert.Equal(2, Own.InARow(comments, Markers.Broke, "harness"));
    }

    [Fact]
    public void AHandsLiftStartsTheCountAgain()
    {
        var comments = new[] { Said(1, $"broke\n{Markers.Broke}"), Said(3, $"broke\n{Markers.Broke}") };

        Assert.Equal(1, Own.InARow(comments, Markers.Broke, "harness", since: T0.AddMinutes(2)));
    }

    [Fact]
    public void AMarkerFromAnyoneElseOrNotOnItsOwnLineIsNotCounted()
    {
        var comments = new[]
        {
            Said(1, $"broke\n{Markers.Broke}", author: "stranger"),
            Said(2, $"a person quotes {Markers.Broke} in a sentence"),
            Said(3, $"broke\n{Markers.Broke}"),
        };

        Assert.Equal(1, Own.InARow(comments, Markers.Broke, "harness"));
        Assert.Equal(1, new Markers("review", ["fleet"]).InARow([Said(1, $"x\n{Markers.Broke}", author: "fleet")], Markers.Broke, "harness"));
        Assert.False(Own.Says(Said(1, $"x\n{Markers.Voyage}", author: "stranger"), Markers.Voyage, "harness"));
    }

    [Fact]
    public void TheNewestCommitsMarkWinsAndALastGreenSkipsTheReds()
    {
        // Newest commit first, as the repository returns them.
        var marks = new[]
        {
            new StatusMark("c3", "harness/suite/shop", false, T0.AddHours(3)),
            new StatusMark("c3", "harness/gate", true, T0.AddHours(3)),
            new StatusMark("c2", "harness/suite/shop", false, T0.AddHours(2)),
            new StatusMark("c1", "harness/suite/shop", true, T0.AddHours(1)),
        };

        Assert.Equal("c3", StatusMark.Newest(marks, "harness/suite/shop")!.Sha);
        Assert.Equal("c1", StatusMark.Newest(marks, "harness/suite/shop", ok: true)!.Sha);
        Assert.Null(StatusMark.Newest(marks, "harness/suite/clinic"));
        Assert.Equal(T0.AddHours(3), StatusMark.LastAt(marks, "harness/gate"));
    }
}
