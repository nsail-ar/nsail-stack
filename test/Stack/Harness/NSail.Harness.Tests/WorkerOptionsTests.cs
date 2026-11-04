// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

// What a workers configuration must say to start, and how a machine's Local changes a shared one.
public class WorkerOptionsTests
{
    static HarnessOptions Bind(params (string Key, string Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => "Harness:" + s.Key, s => (string?)s.Value))
            .Build();
        var o = config.GetSection(HarnessOptions.Section).Get<HarnessOptions>()!;
        o.Seats.TryAdd("builder", new AgentOptions { Signature = "Builder", PromptFile = "p.md" });
        return o;
    }

    static string Problem(params (string, string)[] settings)
    {
        return Assert.Throws<InvalidOperationException>(() => Worker.Compile(Bind(settings))).Message;
    }

    [Fact]
    public void AnUnknownTypeIsNamed()
    {
        Assert.Contains("Harness:Workers:x: no type Deploy", Problem(("Workers:x:Type", "Deploy")));
    }

    [Fact]
    public void AnAgentWorkerNeedsASeat()
    {
        Assert.Contains("needs a Seat", Problem(("Workers:dev:Type", "Develop")));
        Assert.Contains("no seat tim", Problem(("Workers:a:Type", "Analyze"), ("Workers:a:Seat", "tim")));
    }

    [Fact]
    public void AnEventTheTypeDoesNotHaveIsNamed()
    {
        Assert.Contains("has no such event", Problem(("Workers:dev:Type", "Develop"), ("Workers:dev:Seat", "builder"), ("Workers:dev:On:Clean:0:MoveTo", "Merged")));
    }

    [Fact]
    public void AWorkerOnItsOwnHasNoStoryToActOn()
    {
        Assert.Contains("no story to act on", Problem(("Workers:r:Type", "Release"), ("Workers:r:On:Failed:0:Comment", "x")));
        Assert.Contains("no story to act on", Problem(("Workers:v:Type", "Verify"), ("Workers:v:Target", "main"), ("Workers:v:On:Failed:0:Comment", "x")));
    }

    [Fact]
    public void AJobFiresItsOwnEventThenItsVerdictThenExecuted()
    {
        Assert.Equal(["Opened", "Succeeded", "Executed"], JobEvents.Fired(new JobOutcome("PR #1 opened", Event: "Opened")));
        Assert.Equal(["Failed", "Executed"], JobEvents.Fired(new JobOutcome("dead voyage", Failed: true)));
        Assert.Equal(["Findings", "Failed", "Executed"], JobEvents.Fired(new JobOutcome("fourth read", Event: "Findings", Failed: true)));
        // The quota wall and nothing to do are no story's doing: no event.
        Assert.Empty(JobEvents.Fired(new JobOutcome("quota", QuotaUntil: DateTimeOffset.UtcNow)));
        Assert.Empty(JobEvents.Fired(new JobOutcome("closed", Idle: true)));
    }

    [Fact]
    public void AWorkersActionsRunForWhatFiredInOrder()
    {
        var o = Bind(("Workers:dev:Type", "Develop"), ("Workers:dev:Seat", "builder"),
            ("Workers:dev:On:Executed:0:Comment", "done"),
            ("Workers:dev:On:Opened:0:AddLabel", "built"),
            ("Workers:dev:On:Failed:0:AddLabel", "sad"));
        var dev = Worker.Compile(o).Single();

        var actions = JobEvents.Actions(dev.On, new JobOutcome("PR #1 opened", Event: "Opened"));

        Assert.Equal(["Opened: AddLabel built", "Executed: Comment"], actions.Select(a => $"{a.Event}: {a.Action}"));
    }

    [Fact]
    public void AnActionDoesOneThing()
    {
        Assert.Contains("exactly one", Problem(("Workers:k:Type", "UpdateBoard"), ("Workers:k:On:Executed:0:MoveTo", "Ready"), ("Workers:k:On:Executed:0:Comment", "x")));
    }

    [Fact]
    public void AnUpdateBoardWithNothingToDoIsRefused()
    {
        Assert.Contains("does nothing", Problem(("Workers:k:Type", "UpdateBoard")));
    }

    [Fact]
    public void FilterValuesAreChecked()
    {
        Assert.Contains("Filter Pr", Problem(("Workers:k:Type", "UpdateBoard"), ("Workers:k:Filter:Pr", "maybe"), ("Workers:k:On:Executed:0:MoveTo", "Ready")));
        Assert.Contains("no state green", Problem(("Workers:k:Type", "UpdateBoard"), ("Workers:k:Filter:Checks", "red|green"), ("Workers:k:On:Executed:0:MoveTo", "Ready")));
        Assert.Contains("Title", Problem(("Workers:k:Type", "UpdateBoard"), ("Workers:k:Filter:Title", "("), ("Workers:k:On:Executed:0:MoveTo", "Ready")));
    }

    [Fact]
    public void ANamedConditionMustExist()
    {
        Assert.Contains("When uses cpu-free", Problem(("Workers:dev:Type", "Develop"), ("Workers:dev:Seat", "builder"), ("Workers:dev:When:Use:0", "cpu-free")));
    }

    [Fact]
    public void ALocalChangesOneWorkerByNameAndTurnsAnotherOff()
    {
        var shared = new Dictionary<string, string?>
        {
            ["Harness:Workers:develop:Type"] = "Develop",
            ["Harness:Workers:develop:Seat"] = "builder",
            ["Harness:Workers:develop:Priority"] = "90",
            ["Harness:Workers:review:Type"] = "Review",
            ["Harness:Workers:review:Seat"] = "builder",
            ["Harness:Workers:review:Priority"] = "40",
        };
        var local = new Dictionary<string, string?>
        {
            ["Harness:Workers:develop:Max"] = "1",
            ["Harness:Workers:review:Enabled"] = "false",
        };
        var o = new ConfigurationBuilder().AddInMemoryCollection(shared).AddInMemoryCollection(local).Build()
            .GetSection(HarnessOptions.Section).Get<HarnessOptions>()!;
        o.Seats["builder"] = new AgentOptions { PromptFile = "p.md" };

        var workers = Worker.Compile(o);

        var develop = Assert.Single(workers);
        Assert.Equal(("develop", 1, 90), (develop.Name, develop.Options.Max, develop.Priority));
    }

    [Fact]
    public void AWorkersFilterLaysOverItsTypes()
    {
        var o = Bind(("Workers:round:Type", "Develop"), ("Workers:round:Seat", "builder"), ("Workers:round:Filter:Pr", "open"), ("Workers:round:Filter:Checks", "red"));

        var round = Worker.Compile(o).Single();

        Assert.Equal(("open", "red", "Ready"), (round.Filter.Pr, round.Filter.Checks, round.Filter.Status));
        Assert.False(round.TakesNew);
    }

    [Theory]
    [InlineData("Ready", "Ready", true)]
    [InlineData("Ready|In-Progress", "In-Progress", true)]
    [InlineData("!Closed", "Ready", true)]
    [InlineData("!Backlog|none", "-", false)]
    [InlineData("none", "-", true)]
    public void StatusTerms(string status, string stage, bool holds)
    {
        Assert.Equal(holds, Worker.StatusHolds(status, stage));
    }

    [Theory]
    [InlineData("ai", "ai,p1", true)]
    [InlineData("!ruling", "ai", true)]
    [InlineData("!ruling", "ruling", false)]
    [InlineData("ai|ai-dev", "ai-dev", true)]
    [InlineData("ai|ai-dev", "p1", false)]
    public void LabelTerms(string term, string labels, bool holds)
    {
        Assert.Equal(holds, Worker.LabelHolds(term, labels.Split(',').ToHashSet()));
    }

    [Fact]
    public void AWorkersMaxCapsItsJobsRunningAndPicked()
    {
        var plan = new[]
        {
            new WorkItem(WorkKind.Dev, 1, null, "rest", "rest", Worker: "develop"),
            new WorkItem(WorkKind.Dev, 2, null, "rest", "rest", Worker: "develop"),
            new WorkItem(WorkKind.Analyze, 3, null, "analyze", "analyze", Worker: "analyze"),
        };

        var picked = Capacity.Pick(plan, new Dictionary<Resource, int>(), new Limits(5, 2, 1),
            new Dictionary<string, int> { ["develop"] = 2 }, new Dictionary<string, int> { ["develop"] = 1 });

        Assert.Equal([1, 3], picked.Select(p => p.Story!.Value));
    }
}

public class EventDefaultsTests
{
    static List<string> Of(WorkKind kind, JobOutcome outcome)
    {
        return JobEvents.Actions(WorkerTypes.Of(kind)!.Defaults, outcome).Select(a => $"{a.Event}: {a.Action}").ToList();
    }

    // What the jobs used to write themselves, now each event's default: a job no worker planned ends the same.
    [Fact]
    public void TheJobsEndingsMoveTheStoryAsBefore()
    {
        Assert.Equal(["Ready: MoveTo Ready", "Ready: RemoveLabel stalled"], Of(WorkKind.Analyze, new JobOutcome("Ready", Event: "Ready")));
        Assert.Equal(["Ruling: AddLabel ruling", "Ruling: RemoveLabel stalled"], Of(WorkKind.Analyze, new JobOutcome("ruling", Event: "Ruling")));
        Assert.Equal(["Ruling: AddLabel ruling", "Ruling: MoveTo Analyze"], Of(WorkKind.Dev, new JobOutcome("ruling", Event: "Ruling")));
        Assert.Equal(["HandedOff: MoveTo Ready"], Of(WorkKind.Dev, new JobOutcome("handed off", Event: "HandedOff")));
        Assert.Equal(["Landed: MoveTo Merged"], Of(WorkKind.Dev, new JobOutcome("landed", Event: "Landed")));
        Assert.Empty(Of(WorkKind.Dev, new JobOutcome("PR #1 opened", Event: "Opened")));
        Assert.Equal(["Clean: MoveTo Merged", "Clean: RemoveLabel claimed,stalled"], Of(WorkKind.Review, new JobOutcome("landed", Event: "Clean")));
        Assert.Equal(["StillOpen: RemoveLabel claimed", "StillOpen: MoveTo Ready"], Of(WorkKind.Review, new JobOutcome("merged", Event: "StillOpen")));
        Assert.Equal(["Findings: MoveTo Ready"], Of(WorkKind.Review, new JobOutcome("findings", Event: "Findings")));
        Assert.Equal(["Ruling: AddLabel ruling", "Ruling: MoveTo Analyze"], Of(WorkKind.Review, new JobOutcome("ruling", Event: "Ruling")));
        // A fourth read stalls inside the job: no Findings event, so nothing sends it back to Ready.
        Assert.Empty(Of(WorkKind.Review, new JobOutcome("fourth read", Failed: true)));
    }

    [Fact]
    public void AWorkerReplacesAnEventWholeAndKeepsTheOthers()
    {
        var o = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Harness:Workers:review:Type"] = "Review",
            ["Harness:Workers:review:Seat"] = "v",
            ["Harness:Workers:review:On:Clean:0:MoveTo"] = "Done",
        }).Build().GetSection(HarnessOptions.Section).Get<HarnessOptions>()!;
        o.Seats["v"] = new AgentOptions { PromptFile = "p.md" };

        var review = Worker.Compile(o).Single();

        Assert.Equal(["Clean: MoveTo Done"], JobEvents.Actions(review.On, new JobOutcome("landed", Event: "Clean")).Select(a => $"{a.Event}: {a.Action}"));
        Assert.Equal(["Findings: MoveTo Ready"], JobEvents.Actions(review.On, new JobOutcome("findings", Event: "Findings")).Select(a => $"{a.Event}: {a.Action}"));
    }
}
