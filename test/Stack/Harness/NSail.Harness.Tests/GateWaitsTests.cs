// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A PR's red that is all main's own red waits for main to move instead of sending a round.
public class GateWaitsTests
{
    [Fact]
    public void TheSameFailuresInTwoSlotsReadAlike()
    {
        var main = "/w/slots/slot-1/test/A/H.cs(84,42): error CS9035: Required member 'X.Name' [/w/slots/slot-1/test/A/A.csproj]\n  Failed NSail.A.T.One [57 ms]";
        var pr = "/w/slots/slot-3/test/A/H.cs(84,42): error CS9035: Required member 'X.Name' [/w/slots/slot-3/test/A/A.csproj]\n  Failed NSail.A.T.One [2 s]";

        Assert.Equal(GateJob.FailureSet(main, "/w/slots/slot-1"), GateJob.FailureSet(pr, "/w/slots/slot-3"));
    }

    [Fact]
    public void AHeadWaitingOnMainIsPendingUntilMainMoves()
    {
        string[] bodies = [$"red only where main is red\n{Markers.GateWaits("aaa", "c1")}"];

        Assert.Equal(CiState.Pending, Markers.GateAt(bodies, "aaa", "c1"));
        Assert.Null(Markers.GateAt(bodies, "aaa", "c2"));
        Assert.Equal(CiState.Red, Markers.GateAt([.. bodies, Markers.Gate(false, "aaa")], "aaa", "c1"));
    }

    [Fact]
    public void OnlyTheReadsOfThePrsOwnFilesAreItsRed()
    {
        Assert.True(GateJob.IsOwnRed(new System.Xml.XmlException("bad project")));
        Assert.True(GateJob.IsOwnRed(new FileNotFoundException("no solution")));
        Assert.True(GateJob.IsOwnRed(new DirectoryNotFoundException("no folder")));
        Assert.False(GateJob.IsOwnRed(new InvalidOperationException("git fetch: timed out")));
        Assert.False(GateJob.IsOwnRed(new IOException("disk full")));
    }

    [Fact]
    public void AnUnreadableFileIsNamedRelativeToTheCheckout()
    {
        var slot = Path.Combine(Path.GetTempPath(), "gate", "slot-3");
        var message = new FileNotFoundException($"Could not find file '{Path.Combine(slot, "NSail.sln")}'.").Message;

        var said = GateJob.InCheckout(message, slot);

        Assert.Equal("Could not find file 'NSail.sln'.", said);
    }
}
