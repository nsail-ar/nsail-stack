// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSail.Harness.GitHub;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Locks;
using NSail.Harness.Planning;
using NSail.Harness.Watching;

namespace NSail.Harness.Tests;

// A job that ends on the provider's limit or its server's failure is not the story's break: nothing
// is said on the story, nothing counts toward stalled, the story goes back as a break puts it, and
// the item waits what the provider asked. Any other ending still breaks.
public sealed class JobLimitTests : IDisposable
{
    const int Story = 5;
    readonly string _root = Directory.CreateTempSubdirectory("job-limit").FullName;
    readonly MemoryBoard _board = new();
    readonly Locks _locks = new();
    readonly JobHistory _history;
    readonly HarnessMetrics _metrics = new();
    readonly HarnessLoop _loop;

    public JobLimitTests()
    {
        _history = new JobHistory(Path.Combine(_root, "history"));
        _loop = new HarnessLoop(null!, _board, _locks, new Workspace(Path.Combine(_root, "ws"), "https://example.com/acme/app.git", null, 1, "Gru", "gru@example.com"),
            _history, new JobStats(null), null!, new BuildLeases(1), null!, Options.Create(new HarnessOptions { Machine = "box" }), null!, _metrics,
            new WatchState(null), new HarnessState(Path.Combine(_root, "state")), NullLogger<HarnessLoop>.Instance);
        _board.Add(Story, stage: Stages.InProgress, labels: [Labels.Claimed]);
    }

    public void Dispose()
    {
        _metrics.Dispose();
        Directory.Delete(_root, true);
    }

    static WorkItem Dev(int? pr = null)
    {
        return new WorkItem(WorkKind.Dev, Story, pr, "rest", "rest", Worker: "dev");
    }

    static GitHubException Limited(TimeSpan wait)
    {
        return new GitHubException(HttpStatusCode.Forbidden, "403 GET /repos/acme/app/pulls/287/files: API rate limit exceeded for installation", 0, wait);
    }

    static GitHubException Status(HttpStatusCode status)
    {
        return new GitHubException(status, $"{(int)status} GET /repos/acme/app/pulls?state=closed: oops");
    }

    async Task<JobRecord> End(Exception e, WorkItem? item = null)
    {
        item ??= Dev();
        var id = $"job-{Guid.NewGuid():N}";
        await _loop.Sail(new Throws(item, e), new RunningJob(id, item, 1, DateTimeOffset.UtcNow, Path.Combine(_root, $"{id}.log")));
        return _history.Jobs().Last(j => j.Id == id && j.Ended is not null);
    }

    int Broke()
    {
        return _board.Cards[Story].Comments.Count(c => c.Body.Contains(Markers.Broke, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALimitedEndingSaysNothingAndDoesNotCountTowardStalled()
    {
        await End(Limited(TimeSpan.FromMinutes(42)));

        Assert.Empty(_board.Cards[Story].Comments);
        Assert.DoesNotContain(Labels.Stalled, _board.Cards[Story].Labels);

        await End(new InvalidOperationException("boom"));

        Assert.Equal(1, Broke());
        Assert.DoesNotContain(Labels.Stalled, _board.Cards[Story].Labels);
    }

    [Fact]
    public async Task A5xxEndingSaysNothingAndDoesNotCountTowardStalled()
    {
        await End(Status(HttpStatusCode.BadGateway));
        await End(Status(HttpStatusCode.ServiceUnavailable));

        Assert.Empty(_board.Cards[Story].Comments);
        Assert.DoesNotContain(Labels.Stalled, _board.Cards[Story].Labels);
    }

    [Fact]
    public async Task AProviderEndingPutsTheStoryBackAsABreakDoes()
    {
        await End(Limited(TimeSpan.FromMinutes(3)));

        Assert.DoesNotContain(Labels.Claimed, _board.Cards[Story].Labels);
        Assert.Equal(Stages.Ready, _board.Cards[Story].Stage);
        Assert.Equal(new[] { Story }, _locks.Released);

        var review = _board.Add(6, stage: Stages.InProgress, labels: [Labels.Review]);
        await End(Status(HttpStatusCode.BadGateway), new WorkItem(WorkKind.Review, 6, 9, "rest", "rest", Worker: "review"));

        Assert.DoesNotContain(Labels.Review, review.Labels);
        Assert.Equal(Stages.InProgress, review.Stage);
        Assert.Equal(new[] { Story, 6 }, _locks.Released);
    }

    [Fact]
    public async Task ALimitedEndingHoldsItsItemAndEveryPassForTheAskedWaitUpToTheCeiling()
    {
        var before = DateTimeOffset.UtcNow;
        await End(Limited(TimeSpan.FromMinutes(3)));

        Assert.True(_loop.BackedOff(Dev()));
        var held = HarnessLoop.HeldFor(_loop.LimitedUntil, before);
        Assert.InRange(held!.Value, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3.5));

        await End(Limited(TimeSpan.FromMinutes(50)));

        Assert.InRange(HarnessLoop.HeldFor(_loop.LimitedUntil, before)!.Value, HarnessLoop.MaxLimitBackoff, HarnessLoop.MaxLimitBackoff + TimeSpan.FromMinutes(1));
        Assert.Null(HarnessLoop.HeldFor(_loop.LimitedUntil, before + HarnessLoop.MaxLimitBackoff + TimeSpan.FromMinutes(1)));
        Assert.Equal(HarnessLoop.MaxLimitBackoff, HarnessLoop.ProviderWait(Limited(TimeSpan.FromHours(2))));
        Assert.Equal(TimeSpan.FromSeconds(30), HarnessLoop.ProviderWait(Limited(TimeSpan.FromSeconds(30))));
    }

    [Fact]
    public async Task A5xxEndingBacksItsItemOffAsABreakAndHoldsNoPass()
    {
        await End(Status(HttpStatusCode.InternalServerError));

        Assert.True(_loop.BackedOff(Dev()));
        Assert.Null(_loop.LimitedUntil);
        Assert.Null(HarnessLoop.ProviderWait(new InvalidOperationException()));
        Assert.Equal(TimeSpan.FromMinutes(10), HarnessLoop.ProviderWait(Status(HttpStatusCode.InternalServerError)));
    }

    [Fact]
    public async Task TheHistoryAndTheMetricTellAProviderEndingFromABreak()
    {
        using var jobs = new Jobs();

        var limited = await End(Limited(TimeSpan.FromSeconds(2512)));
        var down = await End(Status(HttpStatusCode.BadGateway));
        var broke = await End(new InvalidOperationException("boom"));

        Assert.Equal(HarnessLoop.ProviderResult, limited.Result);
        Assert.Equal("rate limited — 403 GET /repos/acme/app/pulls/287/files, retry in 42m", limited.Outcome);
        Assert.Equal(HarnessLoop.ProviderResult, down.Result);
        Assert.Equal("provider answered 502 — 502 GET /repos/acme/app/pulls?state=closed", down.Outcome);
        Assert.Equal("broke", broke.Result);
        Assert.Equal(new[] { "provider", "provider", "broke" }, jobs.Outcomes);
    }

    [Fact]
    public async Task AnyOtherEndingStillBreaksAndTheSecondInARowStalls()
    {
        await End(new GitHubException(HttpStatusCode.Forbidden, "403 PATCH /repos/acme/app/issues/5: Resource not accessible by integration"));

        Assert.Equal(1, Broke());
        Assert.DoesNotContain(Labels.Stalled, _board.Cards[Story].Labels);

        await End(Status(HttpStatusCode.NotFound));

        Assert.Equal(2, Broke());
        Assert.Contains(Labels.Stalled, _board.Cards[Story].Labels);

        _board.Add(7, stage: Stages.InProgress, labels: [Labels.Claimed]);
        await End(new InvalidOperationException("boom"), new WorkItem(WorkKind.Dev, 7, null, "rest", "rest", Worker: "dev"));
        await End(new InvalidOperationException("boom"), new WorkItem(WorkKind.Dev, 7, null, "rest", "rest", Worker: "dev"));

        Assert.Contains(Labels.Stalled, _board.Cards[7].Labels);
    }

    sealed class Throws(WorkItem item, Exception e) : IJob
    {
        public WorkItem Item => item;

        public Task<JobOutcome> Run(int slot, string logPath, CancellationToken ct)
        {
            throw e;
        }
    }

    sealed class Locks : IStoryLock
    {
        public List<int> Released { get; } = [];

        public Task<bool> TryAcquire(int story, LockHolder holder, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<LockHolder?> Holder(int story, CancellationToken ct = default)
        {
            return Task.FromResult<LockHolder?>(null);
        }

        public Task<bool> Release(int story, LockHolder owner, CancellationToken ct = default)
        {
            Released.Add(story);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyDictionary<int, string>> Holders(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
        }
    }

    // harness.jobs as the exporter reads it: the outcome tag of every job that ended in this class's loop.
    sealed class Jobs : IDisposable
    {
        readonly MeterListener _listener = new();

        public List<string?> Outcomes { get; } = [];

        public Jobs()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == HarnessMetrics.MeterName && instrument.Name == "harness.jobs") listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome") lock (Outcomes) Outcomes.Add(tag.Value as string);
                }
            });
            _listener.Start();
        }

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
