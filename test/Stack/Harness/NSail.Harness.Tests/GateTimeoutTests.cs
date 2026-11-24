// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging.Abstractions;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Harness.Workers;

namespace NSail.Harness.Tests;

// A gate that runs out its clock while the kernel reports the machine saturated is the machine's
// fault once per head: said as infrastructure, with no verdict and no status. The second, or one on
// a calm machine, is the PR's red.
public class GateTimeoutTests
{
    const int Pr = 9;
    const string Sha = "3be6af53aaaaaaaa";
    const string Main = "42055080bbbbbbbb";

    static readonly PressureReading Saturated = new(23.4, 12);
    static readonly PressureReading Calm = new(0.5, 12);
    static readonly ShellResult TimedOut = new(-1, "", TimedOut: true);

    [Fact]
    public async Task ATimeoutUnderPressureIsTheMachinesBrokeWithNoVerdictAndNoStatus()
    {
        var (job, repo) = Gate(Saturated);

        var e = await Assert.ThrowsAsync<GateJob.MachineSaturated>(() => Judge(job));

        var said = Assert.Single(repo.Said);
        Assert.Contains("broke at `3be6af53`", said.Body);
        Assert.Contains("passed the 45 min while the machine was saturated: memory full avg60 23.4 ≥ 10", said.Body);
        Assert.Contains(Markers.GateSaturated(Sha), said.Body);
        Assert.Null(Markers.GateAt([said.Body], Sha));
        Assert.Empty(repo.Statuses);
        Assert.Contains("memory full avg60 23.4 ≥ 10", e.Message);
    }

    [Fact]
    public async Task ASecondTimeoutUnderPressureAtTheSameHeadIsRed()
    {
        var (job, repo) = Gate(Saturated);
        await Assert.ThrowsAsync<GateJob.MachineSaturated>(() => Judge(job));

        var outcome = await Judge(job);

        Assert.True(outcome.Failed);
        Assert.Equal("red — passed the 45 min", outcome.Summary);
        Assert.Equal(CiState.Red, Markers.GateAt(repo.Said.Select(s => s.Body), Sha));
        Assert.Equal(new[] { "failure" }, repo.Statuses.Select(s => s.State));
    }

    [Fact]
    public async Task ATimeoutOnACalmMachineIsRed()
    {
        foreach (var reading in new[] { Calm, null })
        {
            var (job, repo) = Gate(reading);

            var outcome = await Judge(job);

            Assert.True(outcome.Failed);
            Assert.Equal("red — passed the 45 min", outcome.Summary);
            var said = Assert.Single(repo.Said);
            Assert.Contains(Markers.Gate(false, Sha), said.Body);
            Assert.DoesNotContain("harness:gate:saturated", said.Body);
        }
    }

    // Only the harness's own word counts: a saturated marker someone else wrote buys no second wait.
    [Fact]
    public async Task ASaturatedMarkerFromAnotherAuthorIsNotTheFirstTimeout()
    {
        var (job, repo) = Gate(Saturated);
        repo.Others.Add(new PrNote(DateTimeOffset.UtcNow, Markers.GateSaturated(Sha), "someone"));

        await Assert.ThrowsAsync<GateJob.MachineSaturated>(() => Judge(job));
    }

    static Task<JobOutcome> Judge(GateJob job)
    {
        return job.Judge(Pr, Sha, Main, Path.GetTempPath(), TimedOut, NullLogger.Instance, CancellationToken.None);
    }

    static (GateJob, PrThread) Gate(PressureReading? reading)
    {
        var options = new HarnessOptions
        {
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Workers = { ["gate"] = new WorkerOptions { Type = "Verify", Minutes = 45 } },
        };
        var repo = new PrThread();
        var ctx = new JobContext(new MemoryBoard(), new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace>(), new NoAgent(), options, [],
            NullLoggerFactory.Instance, new BuildLeases(1))
        {
            Pressure = new FixedPressure(reading),
        };
        return (new GateJob(new WorkItem(WorkKind.Gate, 5, Pr, "gate", "no gate at this head", Worker: "gate"), ctx), repo);
    }

    // The PR's thread as the harness wrote it, and the statuses it set.
    internal sealed class PrThread() : DevJobRunTests.FileRepository("unused")
    {
        public List<PrNote> Others { get; } = [];

        public List<(string Sha, string State)> Statuses { get; } = [];

        public override Task<IReadOnlyList<PrNote>> Thread(int pr, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PrNote>>([.. Others, .. Said.Where(s => s.Pr == pr).Select(s => new PrNote(DateTimeOffset.UtcNow, s.Body, "harness"))]);
        }

        public override Task SetStatus(string sha, string context, string state, string description, CancellationToken ct = default)
        {
            Statuses.Add((sha, state));
            return Task.CompletedTask;
        }
    }
}

// A pressure reading the test sets.
sealed class FixedPressure(PressureReading? reading) : IPressure
{
    public PressureReading? Read()
    {
        return reading;
    }
}
