// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NSail.Harness.Board;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Locks;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Harness.Workers;

namespace NSail.Harness;

// Reads the board, plans, and starts what fits on this machine. A finished job wakes the next
// pass; with nothing running and nothing to take, the board is read on a short poll. In Debug
// it only plans and starts nothing.
public sealed class HarnessLoop(
    SnapshotReader reader,
    IBoard board,
    IStoryLock locks,
    Workspace workspace,
    JobHistory history,
    JobStats stats,
    JobContext jobs,
    BuildLeases leases,
    LoadSamples load,
    IOptions<HarnessOptions> options,
    IHostApplicationLifetime lifetime,
    HarnessMetrics metrics,
    Watching.WatchState watches,
    ILogger<HarnessLoop> log) : BackgroundService
{
    // A job that found nothing to do (a story closed, held, already landed) is not sent again
    // for a while: the next pass would plan it again, and the loop would read the board every
    // few seconds for nothing.
    static readonly TimeSpan Backoff = TimeSpan.FromMinutes(10);

    readonly SemaphoreSlim _wake = new(0);
    readonly Lock _gate = new();
    readonly ConcurrentDictionary<string, RunningJob> _running = new();
    readonly ConcurrentDictionary<string, Task> _sailing = new();
    readonly ConcurrentDictionary<string, DateTimeOffset> _backoff = new();

    // When each item still to start was first planned, and how long each running job waited for it.
    readonly ConcurrentDictionary<string, DateTimeOffset> _planned = new();
    readonly ConcurrentDictionary<string, TimeSpan> _waited = new();
    volatile int _stalled;

    // When this machine last walked each story: a walk that left it where a walk takes it (a walk that
    // could not be done) is not walked again before the next cut. A tenant not running its code yet
    // is asked again after QaJob.NotLiveRecheck instead.
    readonly ConcurrentDictionary<int, DateTimeOffset> _walked = new();
    readonly CancellationTokenSource _jobsStop = new();
    Pass? _last;
    string? _paused;
    DateTimeOffset? _quotaUntil;
    volatile bool _restarting;
    DateTimeOffset? _pausedSince;
    readonly IReadOnlyList<Worker> _workers = Worker.Compile(options.Value);

    public Pass? Last
    {
        get { lock (_gate) return _last; }
    }

    // Off, only what is already started moves: rounds, gates, reviews, QA, releases. A hand flips it
    // (POST /new); the configuration says where a start begins.
    volatile bool _takeNew = options.Value.TakeNew;

    public bool TakeNew
    {
        get => _takeNew;
        set { _takeNew = value; Wake(); }
    }

    public string? Paused
    {
        get
        {
            lock (_gate)
            {
                return _paused;
            }
        }
    }

    // What runs, each with what it is doing now and when its log last moved (JobActivity).
    public IReadOnlyCollection<RunningJob> Running => _running.Values.Select(Activity).ToList();

    RunningJob Activity(RunningJob job)
    {
        JobStep? step;
        try
        {
            step = jobs.StepOf(job.Item);
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException)
        {
            step = null;
        }
        return JobActivity.Read(job, step, jobs.Agent.LongestCommand, leases.Waits(JobContext.HolderOf(job.Item)));
    }

    // The stories labelled stalled on the last board read.
    public int Stalled => _stalled;

    // Until when the agent's account refused work (its quota wall): no agent starts before it.
    public DateTimeOffset? AgentsOffUntil
    {
        get { lock (_gate) return _quotaUntil is { } q && q > DateTimeOffset.UtcNow ? q : null; }
    }

    public void Wake()
    {
        _wake.Release();
    }

    public void Pause(string why)
    {
        lock (_gate)
        {
            _paused = why;
            _pausedSince ??= DateTimeOffset.UtcNow;
        }
        log.LogWarning("paused: {Why}", why);
    }

    // A deploy's restart: nothing new starts, and the host stops by itself the moment the last job
    // ends, for whatever supervises it (systemd, the SCM) to start the binary now in place. Nobody
    // has to come back and finish it.
    public void Restart(string why)
    {
        Pause($"restart: {why}");
        _restarting = true;
        StopIfDrained();
    }

    void StopIfDrained()
    {
        if (!_restarting || !_running.IsEmpty) return;
        log.LogWarning("drained — stopping for the restart");
        // Not zero: a supervisor that restarts only on failure restarts this too.
        Environment.ExitCode = 75;
        lifetime.StopApplication();
    }

    public void Resume()
    {
        _restarting = false;
        lock (_gate)
        {
            _paused = null;
            _pausedSince = null;
            _quotaUntil = null;
        }
        Wake();
    }

    // The running jobs are cancelled and given time to put their stories back (labels, locks,
    // stage) before the host goes: a job cut off mid-cleanup strands its story.
    public override async Task StopAsync(CancellationToken ct)
    {
        await _jobsStop.CancelAsync();
        await base.StopAsync(ct);
        var sailing = _sailing.Values.ToArray();
        if (sailing.Length > 0)
        {
            await Task.WhenAny(Task.WhenAll(sailing), Task.Delay(TimeSpan.FromSeconds(60), CancellationToken.None));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var o = options.Value;
        metrics.PausedFor(() => { lock (_gate) return _pausedSince is { } at ? (DateTimeOffset.UtcNow - at).TotalSeconds : 0; });
        log.LogInformation("harness on {Machine}, mode {Mode}, limits {Agents} agents / {Builds} builds / {Browsers} browsers, gate {Gate}, work in {Root}",
            o.Machine, o.Mode, o.Agents, o.Builds, o.Browsers, o.Gate ? "on" : "off", o.WorkRoot);
        log.LogInformation("workers: {Workers}", string.Join(", ", _workers.Select(w => w.Name)));
        stats.Begun(Version(), DateTimeOffset.UtcNow);

        while (!stop.IsCancellationRequested)
        {
            var pass = await RunPass(o, stop);
            lock (_gate) _last = pass;
            var idle = _running.IsEmpty && pass.WouldTake.Count == 0;
            var wait = TimeSpan.FromSeconds(idle || o.Mode == HarnessMode.Debug ? o.IdlePollSeconds : o.BusyFloorSeconds);
            try
            {
                await _wake.WaitAsync(wait, stop);
                // A burst of wakes (several jobs ending together) folds into one pass.
                while (_wake.CurrentCount > 0) await _wake.WaitAsync(stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
        }
    }

    // Locks nobody holds any more. This machine's that no running job owns died with an earlier
    // run; anyone's past five hours outlived the longest job any harness runs.
    async Task SweepLocks(CancellationToken ct)
    {
        foreach (var n in (await locks.Holders(ct)).Keys)
        {
            if (await locks.Holder(n, ct) is not { } h) continue;
            var age = DateTimeOffset.UtcNow - h.At;
            var mine = _running.Values.Any(r => r.Item.Story == n);
            string? why = null;
            if (h.Machine == options.Value.Machine && !mine) why = "its job died with an earlier run";
            else if (age > TimeSpan.FromHours(5)) why = $"held by {h.Machine} for over five hours";
            if (why is null) continue;
            await locks.Release(n, ct);
            log.LogWarning("#{Story}: lock released — {Why}", n, why);
        }
    }

    // A story closed outside the harness keeps its column until something moves it, and the board
    // shows it there: whatever was closed in the last two days stands in Closed.
    async Task SweepClosed(CancellationToken ct)
    {
        foreach (var (n, stage) in await board.ClosedSince(DateTimeOffset.UtcNow.AddDays(-2), ct))
        {
            if (stage == Stages.Closed) continue;
            await board.SetStage(n, Stages.Closed, ct);
            log.LogInformation("#{Story}: closed but in {Stage} — moved to {Closed}", n, stage, Stages.Closed);
        }
    }

    Flight OwnFlight()
    {
        var items = _running.Values.Select(r => r.Item).ToList();
        return new Flight(
            items.Where(i => i.Kind == WorkKind.Dev).Select(i => i.Story!.Value).ToHashSet(),
            items.Where(i => i.Kind is WorkKind.Review or WorkKind.Gate).Select(i => i.Pr!.Value).ToHashSet(),
            items.Where(i => i.Kind == WorkKind.Analyze).Select(i => i.Story!.Value).ToHashSet(),
            items.Any(i => i.Kind == WorkKind.Qa),
            items.Where(i => i.Kind == WorkKind.Release).Select(i => i.Workflow!).ToHashSet())
        {
            MainGating = items.Any(i => i.Kind == WorkKind.Main),
            TestStories = items.Where(i => i.Kind == WorkKind.Qa && i.Story is not null).Select(i => i.Story!.Value).ToHashSet(),
            ShipTags = items.Where(i => i.Kind == WorkKind.Ship).Select(i => i.Tag!).ToHashSet(),
        };
    }

    // A release worker's day's cut is due from its schedule's last firing until a cut is tried after
    // it. Tried means a cut mark on main, whichever harness left it.
    IReadOnlyList<string> DueReleases(IReadOnlyList<StatusMark> marks, DateTimeOffset now)
    {
        return _workers.Where(w => w.Kind == WorkKind.Release && w.Options.When.Schedule is not null)
            .Where(w => Conditions.LastFiring(w.Options.When.Schedule!, now) is { } fired
                && (StatusMark.LastAt(marks, Markers.CutContext(w.Options.Product!.ToLowerInvariant())) is not { } tried || tried < fired))
            .Select(w => w.Options.Workflow!).ToList();
    }

    // A Watch worker's pass is due its period after the last one read, and never twice at once.
    IReadOnlyList<string> DueWatches(DateTimeOffset now)
    {
        var running = _running.Values.Where(r => r.Item.Kind == WorkKind.Watch).Select(r => r.Item.Worker).ToHashSet();
        return _workers.Where(w => w.Kind == WorkKind.Watch && !running.Contains(w.Name)
                && watches.Due(w.Name, TimeSpan.FromMinutes(w.Options.Every ?? 60), now))
            .Select(w => w.Name).ToList();
    }

    // A job a hand sends (POST /run): it skips the workers' order, filters and the machine's limits,
    // not the story's lock. It runs as the first enabled worker of its kind (a release: of its product).
    public Task<string?> Force(WorkItem item)
    {
        var worker = _workers.FirstOrDefault(w => w.Kind == item.Kind && (item.Kind != WorkKind.Release || string.Equals(w.Options.Product, item.ProductName, StringComparison.OrdinalIgnoreCase)))
            ?? throw new InvalidOperationException($"no worker runs {item.Kind}{(item.ProductName is { } p ? $" for {p}" : "")}");
        return Start(item with { Worker = worker.Name, Workflow = item.Workflow ?? worker.Options.Workflow }, CancellationToken.None);
    }

    Dictionary<Resource, int> InUse()
    {
        return _running.Values.SelectMany(r => Capacity.Needs(r.Item.Kind)).GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
    }

    bool WalkedSinceCut(WorkItem item, DateTimeOffset? lastCut)
    {
        return item is { Kind: WorkKind.Qa, Story: { } n } && _walked.TryGetValue(n, out var at) && (lastCut is null || at > lastCut);
    }

    bool BackedOff(WorkItem item)
    {
        var key = BackoffKey(item);
        if (!_backoff.TryGetValue(key, out var until)) return false;
        if (until > DateTimeOffset.UtcNow) return true;
        _backoff.TryRemove(key, out _);
        return false;
    }

    // A story's work backs off as one; work with no story (a release, main's gate, a QA drain) by
    // its kind and product, or a broken cut would be tried again every pass.
    static string BackoffKey(WorkItem item)
    {
        return item.Story is { } n ? $"#{n}" : $"{item.Kind}:{item.Product ?? item.Workflow}";
    }

    // An item's wait runs from the first pass that planned it; one the board stopped planning starts over.
    void Planned(IReadOnlyList<WorkItem> items, DateTimeOffset now)
    {
        var keys = items.Select(PlannedKey).ToHashSet();
        foreach (var key in keys) _planned.TryAdd(key, now);
        foreach (var key in _planned.Keys.Where(k => !keys.Contains(k)).ToList()) _planned.TryRemove(key, out _);
    }

    static string PlannedKey(WorkItem item)
    {
        return $"{item.Kind}:{item.Story}:{item.Pr}:{item.Product ?? item.Workflow}";
    }

    // The commit the harness was built from (the SDK stamps it after the version's `+`).
    static string? Version()
    {
        var info = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(HarnessLoop).Assembly)?.InformationalVersion;
        return info?.IndexOf('+') is { } plus and >= 0 ? info[(plus + 1)..][..Math.Min(7, info.Length - plus - 1)] : null;
    }

    async Task<Pass> RunPass(HarnessOptions o, CancellationToken stop)
    {
        var started = DateTimeOffset.UtcNow;
        var call = PassCall.Planning;
        try
        {
            if (leases.Sweep(TimeSpan.FromMinutes(60)) is var dead and > 0) log.LogWarning("{Dead} build turn(s) older than an hour went back to the line", dead);
            call = PassCall.Statuses;
            var marks = await jobs.Repo.Statuses(ct: stop);
            call = PassCall.Stories;
            var read = await reader.Read(OwnFlight(), stop);
            call = PassCall.Planning;
            var snapshot = read with
            {
                DueReleases = DueReleases(marks, DateTimeOffset.UtcNow),
                DueWatches = DueWatches(DateTimeOffset.UtcNow),
                // A red main whose failures this harness does not hold (it restarted since) is judged
                // again, once: a PR's gate tells main's red from its own by them.
                MainGated = StatusMark.Newest(marks, Markers.GateContext) is { } gated && (gated.Ok || MainGateJob.RedAt(gated.Sha) is not null) ? gated.Sha : null,
                LastQaAt = StatusMark.LastAt(marks, Markers.QaContext),
            };
            var limits = new Limits(o.Agents, o.Builds, o.Browsers);
            leases.Limit = limits.Builds;
            var conditions = _workers.SelectMany(w => w.Options.When.Use.Select(u => o.Conditions[u]).Append(w.Options.When));
            var down = await Down(conditions.SelectMany(c => c.Reachable).Distinct(), stop);
            if (down.Count > 0) log.LogWarning("{Down} not answering: the workers that need it wait", string.Join(", ", down));
            var plan = WorkerPlanner.Make(snapshot, _workers, PlannerOptionsOf(o, limits.Agents), w => WhenHolds(w, o, down));
            _stalled = snapshot.Issues.Count(i => i.Has(Labels.Stalled));
            Planned(plan.Items, started);
            var runnable = plan.Items.Where(i => !BackedOff(i) && !WalkedSinceCut(i, snapshot.LastCutAt) && (TakeNew || !Capacity.Starting.Contains(i.Tier)));
            // Past the agent's quota wall only the agents stop: gates, builds and releases go on.
            if (AgentsOffUntil is not null) limits = limits with { Agents = 0 };
            var take = Paused is null ? Capacity.Pick(runnable, InUse(), limits, WorkerMax(), RunningByWorker()) : [];
            var startedNow = new List<string>();
            if (o.Mode == HarnessMode.Live)
            {
                call = PassCall.Locks;
                await SweepLocks(stop);
                call = PassCall.Closed;
                await SweepClosed(stop);
                call = PassCall.Chores;
                await BoardChores.Do(board, plan.Chores, BoardChores.ReadOf(snapshot.Issues), o.Texts.Signature, log, stop);
                call = PassCall.Start;
                if (Paused is null)
                {
                    foreach (var item in take)
                    {
                        if (await Start(item, stop) is { } id) startedNow.Add($"{item} [{id}]");
                    }
                }
            }
            var took = DateTimeOffset.UtcNow - started;
            log.LogInformation("pass: {Items} planned, {Take} fit, {Started} started, {Running} running, {Took:F1}s",
                plan.Items.Count, take.Count, startedNow.Count, _running.Count, took.TotalSeconds);
            metrics.Passed(failed: false);
            return new Pass(started, took, plan, take, startedNow, null);
        }
        catch (Exception e) when (!stop.IsCancellationRequested)
        {
            return Failed(log, metrics, e, call, started);
        }
    }

    // A failed read starts nothing: a half board reads as an empty queue. A request that timed out
    // lands here too (it is a cancellation, but not the host's). What the provider answered rides
    // the record as numbers and an enum, the only kinds the log scrubber lets through; its body
    // stays in the exception's message, which reaches the console and never the log store.
    internal const string FailedAnswered = "pass failed — nothing started: {ProviderCall} answered {ProviderStatus}, {RateLimitRemaining} calls left";

    internal static Pass Failed(ILogger log, HarnessMetrics metrics, Exception e, PassCall call, DateTimeOffset started)
    {
        if (e is ProviderException { Status: { } status } provider)
        {
            log.LogError(e, FailedAnswered, call, status, provider.RateLimitRemaining);
            metrics.Passed(failed: true, status);
        }
        else
        {
            log.LogError(e, "pass failed — nothing started");
            metrics.Passed(failed: true);
        }
        return new Pass(started, DateTimeOffset.UtcNow - started, null, [], [], e.Message);
    }

    // The required endpoints that refuse a connection within a few seconds.
    static async Task<List<string>> Down(IEnumerable<string> endpoints, CancellationToken ct)
    {
        var down = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var at = endpoint.LastIndexOf(':');
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                await tcp.ConnectAsync(endpoint[..at], int.Parse(endpoint[(at + 1)..]), timeout.Token);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                down.Add(endpoint);
            }
        }
        return down;
    }

    // Whether a worker may start this pass: every condition of its When, and of each named one it uses.
    bool WhenHolds(Worker w, HarnessOptions o, IReadOnlyCollection<string> down)
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new Readings(now, over => load.Cpu(now, over), over => load.Memory(now, over), jobs.Agent.Usage);
        return w.Options.When.Use.Select(u => o.Conditions[u]).Append(w.Options.When).All(c => Conditions.Hold(c, readings, down));
    }

    IReadOnlyDictionary<string, int> WorkerMax()
    {
        return _workers.Where(w => w.Options.Max is not null).ToDictionary(w => w.Name, w => w.Options.Max!.Value);
    }

    IReadOnlyDictionary<string, int> RunningByWorker()
    {
        return _running.Values.Where(r => r.Item.Worker is not null).GroupBy(r => r.Item.Worker!).ToDictionary(g => g.Key, g => g.Count());
    }

    // The planner's knobs, as this project configured them; the WIP limit follows the agents.
    internal static PlannerOptions PlannerOptionsOf(HarnessOptions o, int? agents = null)
    {
        var suite = o.Texts.SuiteRedTitle;
        var slot = suite.IndexOf("{product}", StringComparison.Ordinal);
        return new PlannerOptions(o.Gate)
        {
            WipLimit = o.WipLimit ?? agents ?? o.Agents,
            UnrunnableDrivers = o.UnrunnableDrivers,
            SuiteRedPrefix = slot < 0 ? suite : suite[..slot],
            MainRedPrefix = o.Texts.MainRedTitle,
        };
    }

    async Task<string?> Start(WorkItem item, CancellationToken ct)
    {
        var o = options.Value;
        // A QA drain is no story's: it holds the browser, not a story lock.
        var story = item.Story;
        if (workspace.Take() is not { } slot) return null;
        var id = $"{DateTimeOffset.UtcNow:MMddHHmmss}-{JobContext.HolderOf(item)}";
        var held = false;
        try
        {
            if (story is { } n)
            {
                held = await locks.TryAcquire(n, new LockHolder(o.Machine, id, item.Kind.ToString(), DateTimeOffset.UtcNow), ct);
                if (!held)
                {
                    log.LogInformation("#{Story}: locked by someone else — skipped", n);
                    workspace.Give(slot);
                    return null;
                }
                // The board may have moved since the read: a lock label written meanwhile is someone else's job.
                var labels = await board.Labels(n, ct);
                if (labels.Overlaps(Labels.Locks))
                {
                    log.LogInformation("#{Story}: a lock label appeared since the read — skipped", n);
                    await locks.Release(n, ct);
                    workspace.Give(slot);
                    return null;
                }
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(e, "#{Story}: could not start {Item}", story, item);
            if (held) await Quietly(() => locks.Release(story!.Value, CancellationToken.None), "lock release");
            workspace.Give(slot);
            return null;
        }

        IJob job = item.Kind switch
        {
            WorkKind.Dev => new DevJob(item, jobs, history),
            WorkKind.Review => new ReviewJob(item, jobs, history),
            WorkKind.Gate => new GateJob(item, jobs),
            WorkKind.Main => new MainGateJob(item, jobs),
            WorkKind.Qa => new QaJob(item, jobs, history),
            WorkKind.Release => new ReleaseJob(item, jobs),
            WorkKind.Ship => new ShipJob(item, jobs),
            WorkKind.Watch => new WatchJob(item, jobs, watches),
            _ => new AnalyzeJob(item, jobs, history),
        };
        var running = new RunningJob(id, item, slot, DateTimeOffset.UtcNow, jobs.LogPath(item, DateTimeOffset.UtcNow));
        _running[id] = running;
        history.Record(new JobRecord(id, item.ToString(), story, running.Started, null, null, running.LogPath));
        stats.Started(story, running.Started);
        if (_planned.TryRemove(PlannedKey(item), out var planned)) _waited[id] = running.Started - planned;
        log.LogInformation("→ {Item} on slot {Slot} [{Id}]", item, slot, id);
        _sailing[id] = Task.Run(() => Sail(job, running), CancellationToken.None);
        return id;
    }

    async Task Sail(IJob job, RunningJob running)
    {
        var story = running.Item.Story;
        string outcome;
        JobOutcome? ended = null;
        try
        {
            var result = await job.Run(running.Slot, running.LogPath, _jobsStop.Token);
            outcome = result.Summary;
            ended = result;
            if (running.Item is { Kind: WorkKind.Qa, Story: { } walked })
            {
                if (QaJob.WaitsForTheCut(result)) _walked[walked] = DateTimeOffset.UtcNow;
                else _backoff[BackoffKey(running.Item)] = DateTimeOffset.UtcNow + QaJob.NotLiveRecheck;
            }
            if (result.Idle) _backoff[BackoffKey(running.Item)] = DateTimeOffset.UtcNow + Backoff;
            if (result.QuotaUntil is { } until)
            {
                lock (_gate) _quotaUntil = until;
                log.LogWarning("out of quota until {Until:HH:mm} UTC — no agent starts; builds go on", until);
            }
            // What the job's ending does on the board: its worker's actions, or its type's when the
            // steps planned it. Under the story's lock still: nobody moves it between the two.
            // A comment signs as the worker's seat, when it has one.
            var worker = _workers.FirstOrDefault(w => w.Name == running.Item.Worker);
            var on = worker?.On ?? WorkerTypes.Of(running.Item.Kind)?.Defaults;
            var signature = worker?.Options.Seat is { } seat && options.Value.Seats.TryGetValue(seat, out var s0) && s0.Signature.Length > 0 ? s0.Signature : options.Value.Texts.Signature;
            if (story is { } s && on is not null)
            {
                foreach (var (evt, action) in JobEvents.Actions(on, result))
                {
                    await Quietly(() => JobEvents.Apply(board, s, action, signature, result.Report, CancellationToken.None), $"#{s} on {evt}: {action}");
                }
            }
        }
        catch (Exception e)
        {
            outcome = $"failed: {e.Message}";
            // A job that broke is not sent again on the next pass: it would break the same way, every pass.
            _backoff[BackoffKey(running.Item)] = DateTimeOffset.UtcNow + Backoff;
            log.LogError(e, "{Item} failed", running.Item);
            if (story is { } n)
            {
                // The story goes back where the next pass can take it again: a Dev cut off mid-voyage
                // with no PR would otherwise sit In-Progress, where nothing takes a story.
                var lockLabel = running.Item.Kind switch
                {
                    WorkKind.Dev => Labels.Claimed,
                    WorkKind.Review => Labels.Review,
                    WorkKind.Analyze => Labels.Analyzing,
                    _ => null,
                };
                if (lockLabel is not null) await Quietly(() => board.RemoveLabel(n, lockLabel), $"#{n} {lockLabel} off");
                // Every break is said on the story with its marker; the second in a row is not backed
                // off but stopped, loud: a job that broke twice breaks every ten minutes forever.
                var o = options.Value;
                var kind = running.Item.Kind.ToString().ToLowerInvariant();
                var twice = false;
                if (!_jobsStop.IsCancellationRequested)
                {
                    await Quietly(async () =>
                    {
                        var since = await board.LastLift(n, Labels.Lifts);
                        twice = Markers.InARow(await board.Comments(n), Markers.Broke, since) >= 1;
                        var said = HarnessTexts.Fill(twice ? o.Texts.JobBrokeTwice : o.Texts.JobBroke, ("kind", kind), ("error", e.Message), ("machine", o.Machine));
                        if (twice) await board.AddLabels(n, Labels.Stalled);
                        await board.Comment(n, $"{said} {o.Texts.Signature}\n{Markers.Broke}");
                    }, $"#{n} break said");
                }
                if (twice)
                {
                    outcome += " — broke twice, stalled";
                }
                else if (running.Item.Kind == WorkKind.Dev && running.Item.Pr is null)
                {
                    await Quietly(() => board.SetStage(n, Stages.Ready, CancellationToken.None), $"#{n} back to Ready");
                }
            }
        }
        finally
        {
            if (story is { } n) await Quietly(() => locks.Release(n, CancellationToken.None), $"#{n} lock release");
            leases.ReleaseHolder(JobContext.HolderOf(running.Item));
            workspace.Give(running.Slot);
            _running.TryRemove(running.Id, out _);
            _sailing.TryRemove(running.Id, out _);
        }
        metrics.JobEnded(running.Item.Kind.ToString().ToLowerInvariant(), broke: outcome.StartsWith("failed: ", StringComparison.Ordinal));
        TimeSpan? waited = _waited.TryRemove(running.Id, out var wait) ? wait : null;
        await Quietly(() =>
        {
            stats.Ended(running.Item.Kind, story, running.Started, DateTimeOffset.UtcNow, waited, ended);
            // A review that read its PR clean lands it (ReviewJob.Land): the story's PR merged.
            if (running.Item is { Kind: WorkKind.Review, Pr: { } pr } && ended?.Event == "Clean") stats.Merged(pr, story, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }, "stats");
        var ending = ended is null ? "broke" : ended.Quiet ? "quiet" : ended.Failed ? "failed" : "ok";
        history.Record(new JobRecord(running.Id, running.Item.ToString(), story, running.Started, DateTimeOffset.UtcNow, outcome, running.LogPath, ending));
        log.LogInformation("✓ {Item} [{Id}]: {Outcome}", running.Item, running.Id, outcome);
        StopIfDrained();
        Wake();
    }

    // Cleanup never throws: one failed step must not skip the ones after it.
    async Task Quietly(Func<Task> step, string what)
    {
        try
        {
            await step();
        }
        catch (Exception e)
        {
            log.LogWarning("{What} failed: {Error}", what, e.Message);
        }
    }
}
