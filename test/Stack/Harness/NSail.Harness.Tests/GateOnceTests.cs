// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Repositories;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

// A head is judged once, whichever harness gates it: a gate that finds a verdict at its head when it
// starts stands down, and one whose run ends after another harness judged the head drops its word.
public class GateOnceTests
{
    const int Pr = 4324;
    const string Sha = "78369e06aaaaaaaa";
    const string Main = "42055080bbbbbbbb";

    static readonly ShellResult TimedOut = new(-1, "Build succeeded.\n", TimedOut: true);

    // The PR's one thread, shared by every harness that reads it, and the statuses set on its head.
    sealed class OneThread() : DevJobRunTests.FileRepository("unused")
    {
        public List<(string Sha, string State)> Statuses { get; } = [];

        public override Task<PullRequest> Pr(int number, CancellationToken ct = default)
        {
            return Task.FromResult(new PullRequest(number, "story", true, "story/nsail-4237", Sha, false));
        }

        public override Task<IReadOnlyList<PrNote>> Thread(int pr, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PrNote>>([.. Said.Where(s => s.Pr == pr).Select(s => new PrNote(DateTimeOffset.UtcNow, s.Body, "harness"))]);
        }

        public override Task SetStatus(string sha, string context, string state, string description, CancellationToken ct = default)
        {
            Statuses.Add((sha, state));
            return Task.CompletedTask;
        }
    }

    // One harness's gate job on the shared thread. Its machine has no workspace: a job that builds throws.
    static (GateJob Job, DevJobRunTests.Lines Log) Gate(OneThread repo, string machine)
    {
        var options = new HarnessOptions
        {
            Machine = machine,
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Workers = { ["verify-pr"] = new WorkerOptions { Type = "Verify", Minutes = 45 } },
        };
        var log = new DevJobRunTests.Lines();
        var ctx = new JobContext(new MemoryBoard(), new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace>(),
            new NoAgent(), options, [], log, new BuildLeases(1))
        {
            Pressure = new FixedPressure(null),
        };
        return (new GateJob(new WorkItem(WorkKind.Gate, 4237, Pr, "gate", "no gate at this head", Worker: "verify-pr"), ctx), log);
    }

    static Task<JobOutcome> Judge(GateJob job, ShellResult result, DevJobRunTests.Lines log)
    {
        return job.Judge(Pr, Sha, Main, "/w/slots/slot-1", result, log, CancellationToken.None);
    }

    [Fact]
    public async Task AGateThatFindsAVerdictAtItsHeadBuildsNothingAndSaysNothing()
    {
        foreach (var green in new[] { true, false })
        {
            var repo = new OneThread();
            await repo.Comment(Pr, $"**Gate** at `78369e06`\n{Markers.Gate(green, Sha)}");
            var (job, _) = Gate(repo, "box-b");

            var outcome = await job.Run(1, Path.GetTempFileName(), CancellationToken.None);

            Assert.True(outcome.Idle);
            Assert.Empty(JobEvents.Fired(outcome));
            Assert.Single(repo.Said);
            Assert.Empty(repo.Statuses);
        }
    }

    // Two boxes planned the same head before either judged it: the one that starts after the other's
    // green stands down; the one already building when the green lands drops its run out of time.
    [Fact]
    public async Task OfTwoHarnessesOnOneHeadOnlyTheFirstVerdictIsWritten()
    {
        var repo = new OneThread();
        var (first, firstLog) = Gate(repo, "emma-i9");
        var (late, _) = Gate(repo, "box-b");
        var (building, buildingLog) = Gate(repo, "box-c");

        var judged = await Judge(first, new ShellResult(0, "Build succeeded.\n", TimedOut: false), firstLog);
        var stood = await late.Run(1, Path.GetTempFileName(), CancellationToken.None);
        var dropped = await Judge(building, TimedOut, buildingLog);

        Assert.Equal("green", judged.Summary);
        Assert.True(stood.Idle);
        Assert.True(dropped.Idle);
        Assert.Single(repo.Said);
        Assert.Equal([(Sha, "success")], repo.Statuses);
        Assert.Equal(CiState.Green, Markers.GateAt(repo.Said.Select(s => s.Body), Sha));
    }

    // nsail PR #4324: green at 78369e06, then another box's 45-minute time-out at the same head.
    [Fact]
    public async Task ARunOutOfTimeAtAGreenHeadIsDroppedAndCallsNoHand()
    {
        var repo = new OneThread();
        await repo.Comment(Pr, $"**Gate** green at `78369e06`\n{Markers.Gate(true, Sha)}");
        var (job, log) = Gate(repo, "box-b");

        var outcome = await Judge(job, TimedOut, log);

        Assert.Single(repo.Said);
        Assert.Empty(repo.Statuses);
        Assert.True(outcome.Idle);
        Assert.Null(outcome.Event);
        Assert.Empty(JobEvents.Actions(WorkerTypes.All["Verify"].Defaults, outcome));
        Assert.Equal(CiState.Green, Markers.GateAt(repo.Said.Select(s => s.Body), Sha));
        Assert.Contains(log.Written, l => l.Text.Contains("a run out of time at 78369e06 dropped", StringComparison.Ordinal));
    }

    // Every verdict the job writes reads the thread first: a red run at a head another harness judged says nothing.
    [Fact]
    public async Task ARedRunAtAHeadAnotherHarnessJudgedIsDropped()
    {
        foreach (var green in new[] { true, false })
        {
            var repo = new OneThread();
            await repo.Comment(Pr, $"**Gate** at `78369e06`\n{Markers.Gate(green, Sha)}");
            var (job, log) = Gate(repo, "box-c");

            var outcome = await Judge(job, new ShellResult(1, "  Failed NSail.C.Tests.T.One [12 ms]\n", TimedOut: false), log);

            Assert.True(outcome.Idle);
            Assert.Null(outcome.Event);
            Assert.Single(repo.Said);
            Assert.Empty(repo.Statuses);
            Assert.Contains(log.Written, l => l.Text.Contains("red at 78369e06 dropped", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void AGreenAtAHeadOutlastsALaterRedAtIt()
    {
        string[] bodies = [Markers.Gate(true, Sha), Markers.Gate(false, Sha), Markers.Gate(false, Main)];

        Assert.Equal(CiState.Green, Markers.GateAt(bodies, Sha));
        Assert.Equal(CiState.Green, Markers.VerdictAt(bodies, Sha));
        Assert.Equal(CiState.Red, Markers.GateAt(bodies, Main));
    }

    // Waiting on main is no verdict: the head is gated again once main moves.
    [Fact]
    public void AHeadWaitingOnMainCarriesNoVerdict()
    {
        string[] bodies = [Markers.GateWaits(Sha, Main), $"x\n{Markers.GateSaturated(Sha)}"];

        Assert.Null(Markers.VerdictAt(bodies, Sha));
        Assert.Equal(CiState.Pending, Markers.GateAt(bodies, Sha));
    }

    // A push after a verdict is a new head: the old head's verdict is not its.
    [Fact]
    public void ANewHeadCarriesNoneOfTheOldHeadsVerdict()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(1, "In-Progress").Pr(1, 10, CiState.Pending, gate: null).Build(), new PlannerOptions(Gate: true));

        Assert.Null(Markers.VerdictAt([Markers.Gate(true, Main)], Sha));
        Assert.Contains(plan.Items, i => i.Kind == WorkKind.Gate && i.Why == "no gate at this head");
    }
}
