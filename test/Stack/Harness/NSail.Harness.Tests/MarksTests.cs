// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

public class MarksTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    static StoryComment Said(int minute, string body)
    {
        return new StoryComment(T0.AddMinutes(minute), body, "someone");
    }

    [Fact]
    public void BreaksCountOnlyWhileNothingElseIsSaidBetweenThem()
    {
        var comments = new[]
        {
            Said(1, $"broke {Markers.Broke}"),
            Said(2, "a person looked"),
            Said(3, $"broke {Markers.Broke}"),
            Said(4, $"broke {Markers.Broke}"),
        };

        Assert.Equal(2, Markers.InARow(comments, Markers.Broke));
    }

    [Fact]
    public void AHandsLiftStartsTheCountAgain()
    {
        var comments = new[] { Said(1, $"broke {Markers.Broke}"), Said(3, $"broke {Markers.Broke}") };

        Assert.Equal(1, Markers.InARow(comments, Markers.Broke, since: T0.AddMinutes(2)));
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
