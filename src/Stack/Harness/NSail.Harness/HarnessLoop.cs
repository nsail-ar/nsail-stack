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
using NSail.Problems;

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
    HarnessState state,
    ILogger<HarnessLoop> log) : BackgroundService
{
    // A job that found nothing to do (a story closed, held, already landed) is not sent again
    // for a while: the next pass would plan it again, and the loop would read the board every
    // few seconds for nothing.
    static readonly TimeSpan Backoff = TimeSpan.FromMinutes(10);

    // The longest a provider's rate-limit wait holds the next pass.
    internal static readonly TimeSpan MaxLimitBackoff = TimeSpan.FromMinutes(10);

    // The least a limit holds a pass or a job's item: a `Retry-After` already past, or a reset the machine's
    // clock reads as gone, asks for nothing, and the next call would go out at once and be refused the same way.
    internal static readonly TimeSpan MinLimitBackoff = TimeSpan.FromMinutes(1);

    // A job's result when it ended on the provider's limit or its server's failure, not on its own.
    public const string ProviderResult = "provider";

    readonly SemaphoreSlim _wake = new(0);
    readonly Lock _gate = new();
    readonly string _instance = Guid.NewGuid().ToString("N")[..8];
    // Stories whose lock is being taken or whose job is not yet registered in _running.
    readonly ConcurrentDictionary<int, string> _starting = new();
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
    string? _paused = state.Paused;
    DateTimeOffset? _quotaUntil = state.QuotaUntil;
    // Until when a limited pass or a job's limited ending holds every pass; under _gate, as a job ends on its own task.
    DateTimeOffset? _limitedUntil;
    volatile bool _restarting;
    DateTimeOffset? _pausedSince = state.PausedSince;
    bool _orphansSaid;
    bool _restartPause;
    readonly IReadOnlyList<Worker> _workers = Worker.Compile(options.Value);

    public Pass? Last
    {
        get { lock (_gate) return _last; }
    }

    // Off, only what is already started moves: rounds, gates, reviews, QA, releases. A hand flips it
    // (POST /new); the configuration says where a start begins.
    volatile bool _takeNew = state.TakeNew ?? options.Value.TakeNew;

    public bool TakeNew
    {
        get => _takeNew;
        set
        {
            _takeNew = value;
            lock (_gate) Persist();
            Wake();
        }
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

    internal DateTimeOffset? LimitedUntil
    {
        get { lock (_gate) return _limitedUntil; }
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
            _restartPause = false;
            Persist();
        }
        log.LogWarning("paused: {Why}", why);
    }

    // A deploy's restart: nothing new starts, and the host stops by itself the moment the last job
    // ends, for whatever supervises it (systemd, the SCM) to start the binary now in place. Nobody
    // has to come back and finish it.
    public void Restart(string why)
    {
        _restarting = true;
        lock (_gate)
        {
            if (_paused is null)
            {
                _paused = $"restart: {why}";
                _pausedSince ??= DateTimeOffset.UtcNow;
                _restartPause = true;
            }
        }
        log.LogWarning("paused: restart: {Why}", why);
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

    // Under _gate. The pause a restart itself set is not kept (the next run starts working); a hand's is.
    // Take-new is kept only when a hand moved it off the configuration's.
    void Persist()
    {
        state.Set(_restartPause ? null : _paused, _restartPause ? null : _pausedSince, _quotaUntil, _takeNew == options.Value.TakeNew ? null : _takeNew);
    }

    public void Resume()
    {
        _restarting = false;
        lock (_gate)
        {
            _paused = null;
            _pausedSince = null;
            _quotaUntil = null;
            _restartPause = false;
            Persist();
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
            // A wake ends the wait below, never this hold; a job that ends limited while it holds pushes it further.
            while (HeldFor(LimitedUntil, DateTimeOffset.UtcNow) is { } held)
            {
                log.LogWarning("provider rate limit — next pass in {Seconds:F0}s", held.TotalSeconds);
                try
                {
                    await Task.Delay(held, stop);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    return;
                }
            }
            var pass = await RunPass(o, stop);
            lock (_gate) _last = pass;
            var idle = _running.IsEmpty && pass.WouldTake.Count == 0;
            var wait = NextWait(idle, o);
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

    // Locks nobody holds any more. This instance's that no running or starting job owns died with
    // their job; anyone's past five hours outlived the longest job any harness runs.
    async Task SweepLocks(CancellationToken ct)
    {
        foreach (var n in (await locks.Holders(ct)).Keys)
        {
            if (await locks.Holder(n, ct) is not { } h) continue;
            var owned = _starting.ContainsKey(n) || _running.Values.Any(r => r.Item.Story == n);
            if (SweepReason(h, options.Value.Machine, _instance, owned, DateTimeOffset.UtcNow) is not { } why) continue;
            if (await locks.Release(n, h, ct))
            {
                log.LogWarning("#{Story}: lock released — {Why}", n, why);
            }
        }
    }

    internal static string? SweepReason(LockHolder h, string machine, string instance, bool owned, DateTimeOffset now)
    {
        if (h.Machine == machine && h.Instance == instance && !owned) return "its job died with an earlier run";
        if (now - h.At > TimeSpan.FromHours(5)) return $"held by {h.Machine} for over five hours";
        return null;
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

    // Once, at the first board read: the In-Progress stories no job of this run holds, the ones a
    // crash or a hard kill left waiting for nobody.
    async Task SayOrphans(BoardSnapshot read, CancellationToken ct)
    {
        if (_orphansSaid) return;
        _orphansSaid = true;
        var held = _running.Values.Select(r => r.Item.Story).ToHashSet();
        foreach (var i in read.Issues.Where(i => i.Stage == Stages.InProgress && i.SubIssues == 0 && !held.Contains(i.Number)))
        {
            // Another machine's lock is a job somewhere else; this machine's is a dead run's.
            if (read.LockHolders.ContainsKey(i.Number)
                && await locks.Holder(i.Number, ct) is { } h
                && h.Machine != options.Value.Machine) continue;
            log.LogWarning("#{Story}: In-Progress with no job running ({Claimed}) — nothing holds it", i.Number, i.Has(Labels.Claimed) ? "claimed" : "unclaimed");
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
                && (StatusMark.LastAt(marks, Markers.CutContext(w.Options.Product!.ToLowerInvariant())) is not { } tried || tried < fired)
                && !CutHeld(marks, w.Options.Product!.ToLowerInvariant(), fired, now))
            .Select(w => w.Options.Workflow!).ToList();
    }

    // A harness holds the cut while its newest claim since the firing is unreleased and was renewed inside the lapse.
    internal static bool CutHeld(IReadOnlyList<StatusMark> marks, string product, DateTimeOffset fired, DateTimeOffset now)
    {
        var claim = marks.Where(m => m.Context == Markers.HeldContext(product) && m.At >= fired).OrderByDescending(m => m.At).FirstOrDefault();
        return claim is { Ok: false } && now - claim.At < Markers.ClaimLapse;
    }

    // A Watch worker's pass is due its period after the last one read, and never twice at once.
    IReadOnlyList<string> DueWatches(DateTimeOffset now)
    {
        var running = _running.Values.Where(r => r.Item.Kind == WorkKind.Watch).Select(r => r.Item.Worker).ToHashSet();
        return _workers.Where(w => w.Kind == WorkKind.Watch && !running.Contains(w.Name)
                && watches.Due(w.Name, TimeSpan.FromMinutes((w.Options.Every ?? 60) + (w.Options.IngestLag ?? WatchJob.DefaultIngestLag)), now))
            .Select(w => w.Name).ToList();
    }

    // A job a hand sends (POST api/harness/runs): it skips the workers' order, filters and the machine's
    // limits, not the story's lock, Debug mode, a pause or the quota wall (for a job that needs an agent). It runs as the first enabled worker of its kind (a release: of its product).
    public Task<string?> Force(WorkItem item)
    {
        var worker = _workers.FirstOrDefault(w => w.Kind == item.Kind && (item.Kind != WorkKind.Release || string.Equals(w.Options.Product, item.ProductName, StringComparison.OrdinalIgnoreCase)))
            ?? throw new InvalidOperationException($"no worker runs {item.Kind}{(item.ProductName is { } p ? $" for {p}" : "")}");
        if (ForceRefusal(options.Value.Mode, Paused, AgentsOffUntil, item.Kind) is { } refusal) throw (BusinessException)BusinessProblem.InvalidState(refusal);
        return Start(item with { Worker = worker.Name, Workflow = item.Workflow ?? worker.Options.Workflow }, CancellationToken.None);
    }

    internal static string? ForceRefusal(HarnessMode mode, string? paused, DateTimeOffset? wall, WorkKind kind)
    {
        if (mode == HarnessMode.Debug) return "the harness is in Debug mode: it starts nothing";
        if (paused is { } why) return $"the harness is paused ({why})";
        if (wall is { } until && Capacity.Needs(kind).Contains(Resource.Agent)) return $"out of quota until {WallClock(until, DateTimeOffset.UtcNow)} UTC";
        return null;
    }

    // A wall can last days: past 20 hours the day goes with the hour, as the quota windows' resets do.
    public static string WallClock(DateTimeOffset at, DateTimeOffset now)
    {
        return at - now < TimeSpan.FromHours(20) ? $"{at:HH:mm}" : $"{at:ddd HH:mm}";
    }

    internal static DateTimeOffset LaterWall(DateTimeOffset? held, DateTimeOffset until)
    {
        return held is { } h && h > until ? h : until;
    }

    Dictionary<Resource, int> InUse()
    {
        return _running.Values.SelectMany(r => Capacity.Needs(r.Item.Kind)).GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
    }

    bool WalkedSinceCut(WorkItem item, DateTimeOffset? lastCut)
    {
        return item is { Kind: WorkKind.Qa, Story: { } n } && _walked.TryGetValue(n, out var at) && (lastCut is null || at > lastCut);
    }

    // A story started from the queue: the tiers that start one, and the release's own build or analysis of a story no PR carries yet.
    // `take-new` off holds them all, the andon's included: it is the operator's switch.
    internal static bool StartsStory(WorkItem item)
    {
        return Capacity.Starting.Contains(item.Tier) || (item.Tier == ReleaseAndon.Tier && item is { Kind: WorkKind.Dev or WorkKind.Analyze, Pr: null });
    }

    IReadOnlyList<string> _andon = [];

    // The andon lights and lifts out loud: nothing else says why the harness runs one product's work only.
    void NoteAndon(IReadOnlyList<Andon> lit)
    {
        var now = lit.Select(a => a.Product).Order().ToList();
        foreach (var product in now.Except(_andon)) log.LogWarning("release andon lit ({Product}): only its fix and cut start until it ships", product);
        foreach (var product in _andon.Except(now)) log.LogWarning("release andon lifted ({Product}): the line runs again", product);
        _andon = now;
    }

    internal bool BackedOff(WorkItem item)
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

    // How long another harness's pending gate mark keeps its head from a second gate: the gate's own cut (twice its Minutes, as
    // MainGateJob.Timeout) plus the wait for a build turn; past it, a mark a dead harness left pending holds nothing.
    internal static TimeSpan PendingGate(int minutes)
    {
        return TimeSpan.FromMinutes(minutes * 2) + TimeSpan.FromHours(1);
    }

    // The head main's gate has judged, or is judging on some harness. A red whose failures this harness
    // does not hold (it restarted since) is judged again, once: a PR's gate tells main's red from its own by them.
    internal static string? MainGatedOf(IReadOnlyList<StatusMark> marks, DateTimeOffset now, TimeSpan pendingFor)
    {
        if (StatusMark.NewestOrPending(marks, Markers.GateContext) is not { } gated) return null;
        if (gated.Pending) return now - gated.At < pendingFor ? gated.Sha : null;
        return gated.Ok || MainGateJob.RedAt(gated.Sha) is not null ? gated.Sha : null;
    }

    // The longest any Main worker's gate may run, as another harness's pending mark must outlast it.
    internal static TimeSpan MainPendingFor(IReadOnlyList<Worker> workers)
    {
        return workers.Where(w => w.Kind == WorkKind.Main)
            .Select(w => PendingGate(w.Options.Minutes ?? w.Type.Minutes))
            .DefaultIfEmpty(TimeSpan.Zero).Max();
    }

    // A turn is dead past the longest step any worker runs, with a margin for the job's own wind-down.
    internal static TimeSpan TurnAge(IReadOnlyList<Worker> workers)
    {
        // A main gate holds its turn for twice its step's minutes.
        var longest = workers.Select(w => (w.Options.Minutes ?? w.Type.Minutes) * (w.Kind == WorkKind.Main ? 2 : 1)).DefaultIfEmpty(0).Max();
        return TimeSpan.FromMinutes(longest + 10);
    }

    async Task<Pass> RunPass(HarnessOptions o, CancellationToken stop)
    {
        var started = DateTimeOffset.UtcNow;
        var call = PassCall.Planning;
        try
        {
            if (leases.Sweep(TurnAge(_workers)) is var dead and > 0) log.LogWarning("{Dead} build turn(s) older than the longest step went back to the line", dead);
            call = PassCall.Statuses;
            var marks = await jobs.Repo.Statuses(ct: stop);
            call = PassCall.Stories;
            var read = await reader.Read(OwnFlight(), stop);
            await SayOrphans(read, stop);
            var unjudged = await reader.ReadUnjudged(read.Issues, marks, stop);
            call = PassCall.Planning;
            var snapshot = read with
            {
                DueReleases = DueReleases(marks, DateTimeOffset.UtcNow),
                DueWatches = DueWatches(DateTimeOffset.UtcNow),
                MainGated = MainGatedOf(marks, DateTimeOffset.UtcNow, MainPendingFor(_workers)),
                LastQaAt = StatusMark.LastAt(marks, Markers.QaContext),
                UnjudgedFixes = unjudged,
            };
            var limits = new Limits(o.Agents, o.Builds, o.Browsers);
            leases.Limit = limits.Builds;
            var conditions = _workers.SelectMany(w => w.Options.When.Use.Select(u => o.Conditions[u]).Append(w.Options.When));
            var down = await Down(conditions.SelectMany(c => c.Reachable).Distinct(), stop);
            if (down.Count > 0) log.LogWarning("{Down} not answering: the workers that need it wait", string.Join(", ", down));
            var plan = WorkerPlanner.Make(snapshot, _workers, PlannerOptionsOf(o, limits.Agents), w => WhenHolds(w, o, down));
            _stalled = snapshot.Issues.Count(i => i.Has(Labels.Stalled));
            Planned(plan.Items, started);
            var runnable = plan.Items.Where(i => !BackedOff(i) && !WalkedSinceCut(i, snapshot.LastCutAt) && (TakeNew || !StartsStory(i)));
            NoteAndon(plan.Andon);
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
                await BoardChores.Do(board, ChoresToDo(plan, take), BoardChores.ReadOf(snapshot.Issues), reader.OpenPrOf, o.Texts.Signature, log, stop);
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
            if (e is ProviderException { RetryAfter: { } after })
            {
                lock (_gate) _limitedUntil = LaterWall(_limitedUntil, DateTimeOffset.UtcNow + LimitHold(after));
            }
            return Failed(log, metrics, e, call, started);
        }
    }

    // The Starting chores of the items that fit this pass: an item not started (paused, over capacity,
    // backed off, inside the quota wall) leaves its story as it stands.
    internal static IReadOnlyList<Chore> ChoresToDo(Plan plan, IReadOnlyList<WorkItem> take)
    {
        var starts = take.Where(i => i.Story is not null).Select(i => (i.Story!.Value, i.Worker)).ToHashSet();
        return plan.Chores.Where(c => !WorkerPlanner.IsStarting(c) || starts.Any(s => s.Value == c.Story && c.Why.StartsWith($"{s.Worker}: ", StringComparison.Ordinal))).ToList();
    }

    // The wait a wake ends: the idle poll, or the busy floor while jobs run.
    internal static TimeSpan NextWait(bool idle, HarnessOptions o)
    {
        return TimeSpan.FromSeconds(idle || o.Mode == HarnessMode.Debug ? o.IdlePollSeconds : o.BusyFloorSeconds);
    }

    // How long a limit holds the passes, or a limited job's item: what it asked, between the floor and the ceiling.
    internal static TimeSpan LimitHold(TimeSpan asked)
    {
        return asked < MinLimitBackoff ? MinLimitBackoff : asked < MaxLimitBackoff ? asked : MaxLimitBackoff;
    }

    // What is left of a limit's hold on the passes; a wake does not end it.
    internal static TimeSpan? HeldFor(DateTimeOffset? until, DateTimeOffset now)
    {
        return until is { } u && u > now ? u - now : null;
    }

    // How long an item whose job ended on the provider waits, or null when the ending is a break: a
    // limit announced with its wait (GitHubClient.LimitWait), held as a limited pass is, or a 5xx, the
    // provider's own failure, as long as a break. A bare 403 or a 404 is an answer.
    internal static TimeSpan? ProviderWait(Exception e)
    {
        return e switch
        {
            ProviderException { RetryAfter: { } after } => LimitHold(after),
            ProviderException { Status: >= 500 } => Backoff,
            _ => null,
        };
    }

    // The call it failed on, without the provider's body: `rate limited — 403 GET …/pulls/287/files, retry in 42m`.
    internal static string ProviderOutcome(ProviderException e)
    {
        var call = e.Message.Split(": ", 2)[0];
        return e.RetryAfter is { } after
            ? $"rate limited — {call}, retry in {(after.TotalMinutes >= 1 ? $"{Math.Ceiling(after.TotalMinutes):F0}m" : $"{Math.Ceiling(after.TotalSeconds):F0}s")}"
            : $"provider failed — {call}";
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
            OrphanGrace = TimeSpan.FromMinutes(o.OrphanGraceMinutes),
            UnrunnableDrivers = o.UnrunnableDrivers,
            SuiteRedTitle = suite,
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
        if (story is { } sn && !_starting.TryAdd(sn, id))
        {
            log.LogInformation("#{Story}: another start is in flight — skipped", sn);
            workspace.Give(slot);
            return null;
        }
        // Until the job is in _running the story is _starting's: the sweep must not read its lock as dead.
        RunningJob running;
        try
        {
            var held = false;
            LockHolder? holder = null;
            try
            {
                if (story is { } n)
                {
                    holder = new LockHolder(o.Machine, id, item.Kind.ToString(), DateTimeOffset.UtcNow, _instance);
                    held = await locks.TryAcquire(n, holder, ct);
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
                        await locks.Release(n, holder, ct);
                        workspace.Give(slot);
                        return null;
                    }
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(e, "#{Story}: could not start {Item}", story, item);
                if (held) await Quietly(() => locks.Release(story!.Value, holder!, CancellationToken.None), "lock release");
                workspace.Give(slot);
                return null;
            }

            running = new RunningJob(id, item, slot, DateTimeOffset.UtcNow, jobs.LogPath(item, DateTimeOffset.UtcNow));
            _running[id] = running;
        }
        finally
        {
            if (story is { } sr) _starting.TryRemove(new KeyValuePair<int, string>(sr, id));
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
        try
        {
            history.Record(new JobRecord(id, item.ToString(), story, running.Started, null, null, running.LogPath));
            stats.Started(story, running.Started);
        }
        catch (Exception e)
        {
            log.LogError(e, "#{Story}: bookkeeping failed at the start of {Item} — released", story, item);
            _running.TryRemove(id, out _);
            if (story is { } sl) await Quietly(() => locks.Release(sl, new LockHolder(o.Machine, id, item.Kind.ToString(), running.Started, _instance), CancellationToken.None), $"#{sl} lock release");
            workspace.Give(slot);
            return null;
        }
        if (_planned.TryRemove(PlannedKey(item), out var planned)) _waited[id] = running.Started - planned;
        log.LogInformation("→ {Item} on slot {Slot} [{Id}]", item, slot, id);
        _sailing[id] = Task.Run(() => Sail(job, running), CancellationToken.None);
        return id;
    }

    internal async Task Sail(IJob job, RunningJob running)
    {
        var story = running.Item.Story;
        string outcome;
        JobOutcome? ended = null;
        var provider = false;
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
                DateTimeOffset wall;
                lock (_gate)
                {
                    _quotaUntil = wall = LaterWall(_quotaUntil, until);
                    Persist();
                }
                log.LogWarning("out of quota until {Until} UTC — no agent starts; builds go on", WallClock(wall, DateTimeOffset.UtcNow));
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
        // Not the story's break: nothing is said on it and nothing counts toward stalled; it goes back as a break puts it.
        catch (ProviderException e) when (ProviderWait(e) is not null)
        {
            provider = true;
            outcome = ProviderOutcome(e);
            var until = DateTimeOffset.UtcNow + ProviderWait(e)!.Value;
            _backoff[BackoffKey(running.Item)] = until;
            if (e.RetryAfter is not null)
            {
                lock (_gate) _limitedUntil = LaterWall(_limitedUntil, until);
            }
            log.LogWarning(e, "{Item} ended on the provider: {Outcome}", running.Item, outcome);
            if (story is { } n)
            {
                await LockLabelOff(running.Item, n);
                await BackToReady(running.Item, n);
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
                await LockLabelOff(running.Item, n);
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
                else
                {
                    await BackToReady(running.Item, n);
                }
            }
        }
        finally
        {
            if (story is { } n) await Quietly(() => locks.Release(n, new LockHolder(options.Value.Machine, running.Id, running.Item.Kind.ToString(), running.Started, _instance), CancellationToken.None), $"#{n} lock release");
            leases.ReleaseHolder(JobContext.HolderOf(running.Item));
            workspace.Give(running.Slot);
            _running.TryRemove(running.Id, out _);
            _sailing.TryRemove(running.Id, out _);
        }
        var ending = provider ? ProviderResult : ended is null ? "broke" : ended.Quiet ? "quiet" : ended.Failed ? "failed" : "ok";
        metrics.JobEnded(running.Item.Kind.ToString().ToLowerInvariant(), ending is "broke" or ProviderResult ? ending : "ok");
        TimeSpan? waited = _waited.TryRemove(running.Id, out var wait) ? wait : null;
        await Quietly(() =>
        {
            // A provider's outage is not the crew's work, as the quota wall is not.
            if (!provider) stats.Ended(running.Item.Kind, story, running.Started, DateTimeOffset.UtcNow, waited, ended);
            // A review that read its PR clean lands it (ReviewJob.Land): the story's PR merged.
            if (running.Item is { Kind: WorkKind.Review, Pr: { } pr } && ended?.Event == "Clean") stats.Merged(pr, story, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }, "stats");
        history.Record(new JobRecord(running.Id, running.Item.ToString(), story, running.Started, DateTimeOffset.UtcNow, outcome, running.LogPath, ending));
        log.LogInformation("✓ {Item} [{Id}]: {Outcome}", running.Item, running.Id, outcome);
        StopIfDrained();
        Wake();
    }

    // The story goes back where the next pass can take it again: its job's lock label off.
    async Task LockLabelOff(WorkItem item, int n)
    {
        var lockLabel = item.Kind switch
        {
            WorkKind.Dev => Labels.Claimed,
            WorkKind.Review => Labels.Review,
            WorkKind.Analyze => Labels.Analyzing,
            _ => null,
        };
        if (lockLabel is not null) await Quietly(() => board.RemoveLabel(n, lockLabel), $"#{n} {lockLabel} off");
    }

    // A Dev cut off mid-voyage with no PR goes to Ready, or it would sit In-Progress, where nothing takes a story.
    async Task BackToReady(WorkItem item, int n)
    {
        if (item.Kind == WorkKind.Dev && item.Pr is null)
        {
            await Quietly(() => board.SetStage(n, Stages.Ready, CancellationToken.None), $"#{n} back to Ready");
        }
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
