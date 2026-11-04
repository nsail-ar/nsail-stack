// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

// A walk's verdict file is the Test job's event, and the events do on the board what the walk saw.
public class QaVerdictTests
{
    [Theory]
    [InlineData("VERDICT: ok", "Ok")]
    [InlineData("VERDICT: n/a", "NotApplicable")]
    [InlineData("VERDICT: bug", "Bug")]
    [InlineData("VERDICT: asked", "Asked")]
    [InlineData("VERDICT: waiting", "Waiting")]
    [InlineData("VERDICT: not-live", "NotLive")]
    [InlineData("VERDICT: doubt", "Doubt")]
    [InlineData("VERDICT: maybe", "NoVerdict")]
    [InlineData("the walker wrote no verdict", "NoVerdict")]
    public void TheFirstLineNamesTheEvent(string first, string evt)
    {
        Assert.Equal(evt, QaJob.Verdict(first + "\r\nreport").Event);
    }

    [Fact]
    public void TheReportIsWhatFollowsTheVerdict()
    {
        Assert.Equal("BUG: the total is wrong\n\n1. opened the sale", QaJob.Verdict("VERDICT: bug\nBUG: the total is wrong\n\n1. opened the sale\n").Report);
        Assert.Equal("Ana, mandá un mensaje.", QaJob.Verdict("VERDICT: asked\nASKED: story\n\nAna, mandá un mensaje.\n").Report);
        Assert.Equal("it died", QaJob.Verdict("it died").Report);
    }

    [Fact]
    public void ACommentQuotesTheReportAndSignsUnderIt()
    {
        Assert.Equal("QA:\n\nall good\n\n— Reviewer", JobEvents.Said("QA:\n\n{report}", "— Reviewer", " all good \n"));
        Assert.Equal("done — harness", JobEvents.Said("done", "— harness", null));
    }

    [Fact]
    public void OkClosesTheStoryAndABugGoesBackToAnalyzeCarryingTheMarker()
    {
        var test = WorkerTypes.All["Test"].Defaults;

        Assert.Contains(test["Ok"], a => a.Close is true);
        Assert.Contains(test["Ok"], a => a.MoveTo == Stages.Closed);
        Assert.Contains(test["Bug"], a => a.MoveTo == Stages.Analyze);
        Assert.Contains(test["Bug"], a => a.Comment!.Contains(WorkerTypes.QaBugMarker) == true);
        Assert.Contains(test["BugAgain"], a => a.AddLabel == Labels.Stalled);
        Assert.Contains(test["Asked"], a => a.AddLabel == Labels.QaAsked);
        Assert.False(test.ContainsKey("NotLive"));
    }
}
