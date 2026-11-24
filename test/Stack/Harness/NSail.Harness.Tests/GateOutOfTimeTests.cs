// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Repositories;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

// A gate that passed its time with nothing red is the machine's load, not the PR's red: no failing
// status and no round the first time; red with its count, and a hand's, the second time at one head.
public class GateOutOfTimeTests
{
    const string Sha = "abc1234def";
    const string Main = "0123456789";

    const string Cut = "Build succeeded.\n    0 Warning(s)\n    0 Error(s)\n"
        + "Passed!  - Failed:     0, Passed:    52, Skipped:     0, Total:    52, Duration: 3 s - NSail.A.Tests.dll (net10.0)\n"
        + "Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9, Duration: 40 m - NSail.B.Tests.dll (net10.0)\n";

    sealed class Repo() : DevJobRunTests.FileRepository("")
    {
        public List<PrNote> Before { get; } = [];

        public List<(string Sha, string State)> Statuses { get; } = [];

        public override Task<IReadOnlyList<PrNote>> Thread(int pr, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PrNote>>([.. Before, .. Said.Select(s => new PrNote(DateTimeOffset.UtcNow, s.Body, "harness"))]);
        }

        public override Task SetStatus(string sha, string context, string state, string description, CancellationToken ct = default)
        {
            Statuses.Add((sha, state));
            return Task.CompletedTask;
        }
    }

    static (GateJob Job, Repo Repo) Arrange()
    {
        var options = new HarnessOptions
        {
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Workers = { ["verify-pr"] = new WorkerOptions { Type = "Verify", Minutes = 45 } },
        };
        var repo = new Repo();
        var ctx = new JobContext(new MemoryBoard(), new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace>(),
            new NoAgent(), options, [], new DevJobRunTests.Lines(), new BuildLeases(1));
        return (new GateJob(new WorkItem(WorkKind.Gate, 1, 10, "gate", "no gate at this head", Worker: "verify-pr"), ctx), repo);
    }

    static Task<JobOutcome> Judge(GateJob job, string output, bool timedOut = true)
    {
        return job.Judge(10, Sha, Main, new ShellResult(124, output, TimedOut: timedOut), "/w/slots/slot-1", new DevJobRunTests.Lines(), CancellationToken.None);
    }

    [Fact]
    public async Task AFirstRunOutOfTimeSaysWhatWasGreenAndIsNoVerdict()
    {
        var (job, repo) = Arrange();

        var outcome = await Judge(job, Cut);

        var (pr, body) = Assert.Single(repo.Said);
        Assert.Equal(10, pr);
        Assert.Contains("out of time at `abc1234d`", body, StringComparison.Ordinal);
        Assert.Contains("passed the 45 min with nothing red", body, StringComparison.Ordinal);
        Assert.Contains("the build and 2 test projects green (NSail.A.Tests, NSail.B.Tests)", body, StringComparison.Ordinal);
        Assert.Contains(Markers.GateLate(Sha), body, StringComparison.Ordinal);
        Assert.DoesNotContain(Markers.Gate(false, Sha), body, StringComparison.Ordinal);
        Assert.Null(Markers.GateAt([body], Sha));
        Assert.Equal([(Sha, "pending")], repo.Statuses);
        Assert.True(outcome.Again);
        Assert.Null(outcome.Event);
        Assert.Empty(JobEvents.Fired(outcome));
    }

    [Fact]
    public async Task ASecondRunOutOfTimeAtTheSameHeadIsRedWithItsCountAndCallsAHand()
    {
        var (job, repo) = Arrange();

        await Judge(job, Cut);
        var outcome = await Judge(job, Cut);

        var body = repo.Said[^1].Body;
        Assert.Contains("red at `abc1234d`", body, StringComparison.Ordinal);
        Assert.Contains("out of time 2 times at this head", body, StringComparison.Ordinal);
        Assert.Contains("NSail.A.Tests, NSail.B.Tests", body, StringComparison.Ordinal);
        Assert.Equal(CiState.Red, Markers.GateAt(repo.Said.Select(s => s.Body), Sha));
        Assert.Equal(2, Markers.LateAt(repo.Said.Select(s => s.Body), Sha));
        Assert.Equal((Sha, "failure"), repo.Statuses[^1]);
        Assert.False(outcome.Again);
        Assert.True(outcome.Failed);
        Assert.Equal(GateJob.OutOfTimeEvent, outcome.Event);
        var actions = JobEvents.Actions(WorkerTypes.All["Verify"].Defaults, outcome);
        Assert.Contains(actions, a => a.Action.AddLabel == Labels.Hand);
    }

    [Fact]
    public async Task ARunOutOfTimeAtAnotherHeadDoesNotCount()
    {
        var (job, repo) = Arrange();
        repo.Before.Add(new PrNote(DateTimeOffset.UtcNow, $"out of time\n{Markers.GateLate("fff0000111")}", "harness"));
        repo.Before.Add(new PrNote(DateTimeOffset.UtcNow, $"forged\n{Markers.GateLate(Sha)}", "stranger"));

        var outcome = await Judge(job, Cut);

        Assert.True(outcome.Again);
        Assert.Null(Markers.GateAt(repo.Said.Select(s => s.Body), Sha));
    }

    [Fact]
    public async Task ARunOutOfTimeWithAFailedTestIsRedAsAnyRed()
    {
        var (job, repo) = Arrange();

        var outcome = await Judge(job, Cut + "  Failed NSail.C.Tests.T.One [12 ms]\n");

        Assert.Equal(CiState.Red, Markers.GateAt(repo.Said.Select(s => s.Body), Sha));
        Assert.Contains("build or tests red", repo.Said[^1].Body, StringComparison.Ordinal);
        Assert.Null(outcome.Event);
        Assert.False(outcome.Again);
        Assert.True(outcome.Failed);
    }

    [Fact]
    public void WhatWasGreenIsReadOffTheOutput()
    {
        var texts = new HarnessTexts();

        Assert.Equal("the build green, no test project finished", GateJob.GreenOf("Build succeeded.\n", texts));
        Assert.Equal("the build had not finished", GateJob.GreenOf("  Determining projects to restore...\n", texts));
        Assert.True(GateJob.IsKeptLine("Passed!  - Failed:     0, Passed:    52, Skipped:     0, Total:    52, Duration: 3 s - NSail.A.Tests.dll (net10.0)"));
        Assert.True(GateJob.IsKeptLine("Build succeeded."));
    }

    [Fact]
    public void AHeadOutOfTimeOnceIsGatedAgainAndNoRoundIsSent()
    {
        Assert.Null(Markers.GateAt([Markers.GateWaits(Sha, Main), Markers.GateLate(Sha)], Sha, Main));

        var plan = SampleWorkers.Plan(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Pending, gate: null).Build(), new PlannerOptions(Gate: true));

        Assert.Equal(["gate PR #10 (story #1)"], plan.Items.Select(i => i.ToString()));
    }

    [Fact]
    public void AHeadRedByTimeWaitsForTheHandItCalled()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(1, "In-Progress", Labels.Hand).Pr(1, 10, CiState.Pending, gate: CiState.Red).Build(), new PlannerOptions(Gate: true));

        Assert.Empty(plan.Items);
    }
}
