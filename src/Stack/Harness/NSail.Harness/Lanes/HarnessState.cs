// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// What a hand or the provider set on this machine and a restart must not forget (state.json): the
// pause, the take-new switch, the quota wall and the stories a worker's ending left settled
// (HarnessLoop.Settled). A wall already past is dropped on read.
public sealed class HarnessState
{
    readonly string? _path;
    readonly ILogger? _log;
    readonly Lock _gate = new();
    StateData _data;

    // No root keeps it in memory only.
    public HarnessState(string? root, ILogger? log = null)
    {
        _log = log;
        if (root is null)
        {
            _data = new StateData();
            return;
        }
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "state.json");
        _data = StateFile.Read(_path, () => new StateData(), log: log);
        if (_data.QuotaUntil is { } q && q <= DateTimeOffset.UtcNow) _data.QuotaUntil = null;
    }

    public string? Paused => _data.Paused;

    public DateTimeOffset? PausedSince => _data.PausedSince;

    public DateTimeOffset? QuotaUntil => _data.QuotaUntil;

    public bool? TakeNew => _data.TakeNew;

    // By `worker#story`: when the worker's ending left the story where the worker takes it.
    public IReadOnlyDictionary<string, DateTimeOffset> Settled => _data.Settled;

    public void Set(string? paused, DateTimeOffset? pausedSince, DateTimeOffset? quotaUntil, bool? takeNew)
    {
        lock (_gate)
        {
            _data = new StateData { Paused = paused, PausedSince = pausedSince, QuotaUntil = quotaUntil, TakeNew = takeNew, Settled = _data.Settled };
            Write();
        }
    }

    public void Settle(IReadOnlyDictionary<string, DateTimeOffset> settled)
    {
        lock (_gate)
        {
            _data.Settled = new Dictionary<string, DateTimeOffset>(settled);
            Write();
        }
    }

    // Under _gate.
    void Write()
    {
        if (_path is null) return;
        try
        {
            StateFile.Write(_path, _data);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log?.LogWarning(e, "state file {Path} could not be written — this run keeps it in memory", _path);
        }
    }

    sealed class StateData
    {
        public string? Paused { get; set; }
        public DateTimeOffset? PausedSince { get; set; }
        public DateTimeOffset? QuotaUntil { get; set; }
        public bool? TakeNew { get; set; }
        public Dictionary<string, DateTimeOffset> Settled { get; set; } = [];
    }
}
