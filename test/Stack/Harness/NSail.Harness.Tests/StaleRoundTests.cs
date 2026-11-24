// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSail.Harness.Board;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Harness.Watching;

namespace NSail.Harness.Tests;

// Two machines on one queue: one runs the gate-red round, pushes its fix and takes `claimed` off;
// the other, deciding from a read made before that, neither releases the lock again nor starts the
// round on the red head that is no longer the PR's.
public sealed class StaleRoundTests : IDisposable
{
    const int Story = 5;
    const int Pr = 9;
    const string Red = "3be6af53aaaaaaaa";
    const string Fix = "42055080bbbbbbbb";

    readonly string _root = Directory.CreateTempSubdirectory("stale-round").FullName;
    readonly MemoryBoard _board = new();
    readonly JobLimitTests.Locks _locks = new();
    readonly HarnessMetrics _metrics = new();
    readonly DevJobRunTests.Lines _logs = new();
    readonly Remote _repo = new();
    readonly HarnessLoop _loop;

    public StaleRoundTests()
    {
        var options = new HarnessOptions { Machine = "box", Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        var workspace = new Workspace(Path.Combine(_root, "ws"), "https://example.com/acme/app.git", null, 1, "Gru", "gru@example.com");
        var jobs = new JobContext(_board, new RepositorySet([(_repo, options.Repositories[0])]), new Dictionary<string, Workspace> { [_repo.Name] = workspace }, new NoAgent(), options, [], NullLoggerFactory.Instance, new BuildLeases(1));
        _loop = new HarnessLoop(null!, _board, _locks, workspace,
            new JobHistory(Path.Combine(_root, "history")), new JobStats(null), jobs, new BuildLeases(1), null!, Options.Create(options), new Lifetime(), _metrics,
            new WatchState(null), new HarnessState(Path.Combine(_root, "state")), new Logger<HarnessLoop>(_logs));
    }

    public void Dispose()
    {
        _metrics.Dispose();
        Directory.Delete(_root, true);
    }

    // The PR's head as the code host holds it now.
    sealed class Remote() : DevJobRunTests.FileRepository("unused")
    {
        public string Head { get; set; } = Red;

        public override Task<PullRequest> Pr(int number, CancellationToken ct = default)
        {
            return Task.FromResult(new PullRequest(number, "a story", true, "story/app-5", Head, false));
        }
    }

    // The read machine B planned from: the gate red at Red, and A's `claimed` past the stale-lock age.
    static BoardSnapshot ReadBeforeTheFix()
    {
        return new Fixture().Story(Story, "In-Progress", "claimed").LockAge(Story, Labels.Claimed, TimeSpan.FromMinutes(11))
            .Pr(Story, Pr, gate: CiState.Red, sha: Red).Build();
    }

    [Fact]
    public async Task TheOtherMachineNeitherReleasesTheGoneClaimNorStartsTheStaleRoundNorCountsIt()
    {
        var read = ReadBeforeTheFix();
        var plan = SampleWorkers.Plan(read, new PlannerOptions(Gate: true));
        var round = Assert.Single(plan.Items);
        Assert.Equal(Red, round.Sha);

        // Machine A's round pushed the fix and took its lock off before B writes and starts.
        _board.Add(Story, stage: Stages.InProgress);
        _repo.Head = Fix;

        await BoardChores.Do(_board, HarnessLoop.ChoresToDo(plan, plan.Items), BoardChores.ReadOf(read.Issues),
            (_, _) => Task.FromResult<string?>($"PR #{Pr}"), "— harness", _logs, CancellationToken.None);
        var started = await _loop.Start(round, CancellationToken.None);

        Assert.Null(started);
        Assert.Equal([Story], _locks.Released);
        Assert.Empty(_board.Cards[Story].Comments);
        Assert.Empty(_board.Cards[Story].Labels);
        Assert.Contains(_logs.Written, l => l.Text.Contains($"PR #{Pr} moved to {Fix}", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs.Written, l => l.Text.Contains("no job behind it", StringComparison.Ordinal));
        Assert.Equal(0, DevJob.Count([.. _board.Cards[Story].Comments.Where(c => c.Body.Contains(Markers.Voyage, StringComparison.Ordinal)).Select(c => c.At)], [], null));
    }

    [Fact]
    public async Task OnlyARoundWhosePrHeadMovedSinceItsPlanReadsAsMoved()
    {
        var round = Assert.Single(SampleWorkers.Plan(ReadBeforeTheFix(), new PlannerOptions(Gate: true)).Items);

        Assert.Null(await _loop.MovedSince(round));
        _repo.Head = Fix;
        Assert.Equal(Fix, await _loop.MovedSince(round));
        Assert.Null(await _loop.MovedSince(round with { Kind = WorkKind.Review }));
    }

    sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => default;

        public CancellationToken ApplicationStopping => default;

        public CancellationToken ApplicationStopped => default;

        public void StopApplication()
        {
        }
    }
}
