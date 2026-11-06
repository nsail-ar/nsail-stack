// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// The turns for local builds, a machine's second scarce resource after the AI's requests. A lane
// holds an agent slot for its whole voyage but a build turn only while a `dotnet build|test`
// runs, so the CPU is rationed where it is spent. When a turn frees, it goes to the WAITER OF
// HIGHEST PRIORITY (the lowest rank, the planner's order), never to the one that asked first.
public sealed class BuildLeases(int limit)
{

    sealed record Waiter(int Rank, long Seq, string Holder, TaskCompletionSource<BuildLease> Grant);

    readonly Lock _gate = new();
    readonly List<Waiter> _waiting = [];
    readonly Dictionary<Guid, BuildLease> _held = [];
    long _seq;

    // A plan change moves it: a lower limit revokes nothing (turns held run out), a higher one
    // grants the waiters at once.
    public int Limit
    {
        get { lock (_gate) return _limit; }
        set
        {
            lock (_gate)
            {
                _limit = Math.Max(1, value);
                Grant();
            }
        }
    }

    int _limit = Math.Max(1, limit);

    public int Held
    {
        get { lock (_gate) return _held.Count; }
    }

    public int Waiting
    {
        get { lock (_gate) return _waiting.Count; }
    }

    // Whether this holder stands in line for a turn now.
    public bool Waits(string holder)
    {
        lock (_gate) return _waiting.Any(w => w.Holder == holder);
    }

    public IReadOnlyList<BuildLease> Holders()
    {
        lock (_gate) return _held.Values.OrderBy(l => l.At).ToList();
    }

    // A lower rank is more urgent. Cancelling the wait gives up the place in line.
    public async Task<BuildLease> Acquire(string holder, int rank, CancellationToken ct)
    {
        Waiter w;
        lock (_gate)
        {
            w = new Waiter(rank, _seq++, holder, new TaskCompletionSource<BuildLease>(TaskCreationOptions.RunContinuationsAsynchronously));
            _waiting.Add(w);
            Grant();
        }

        await using var reg = ct.Register(() =>
        {
            lock (_gate)
            {
                if (_waiting.Remove(w)) w.Grant.TrySetCanceled(ct);
            }
        });
        return await w.Grant.Task;
    }

    // A lane killed mid-build never gives its turn back: no build or test run of this repository
    // lasts an hour, so an older turn is dead and goes back to the line.
    public int Sweep(TimeSpan olderThan)
    {
        lock (_gate)
        {
            var stale = _held.Values.Where(l => DateTimeOffset.UtcNow - l.At > olderThan).Select(l => l.Id).ToList();
            foreach (var id in stale) _held.Remove(id);
            if (stale.Count > 0) Grant();
            return stale.Count;
        }
    }

    public int ReleaseHolder(string holder)
    {
        lock (_gate)
        {
            var gone = _held.Values.Where(l => l.Holder == holder).Select(l => l.Id).ToList();
            foreach (var id in gone) _held.Remove(id);
            if (gone.Count > 0) Grant();
            return gone.Count;
        }
    }

    public bool Release(Guid id)
    {
        lock (_gate)
        {
            var had = _held.Remove(id);
            Grant();
            return had;
        }
    }

    void Grant()
    {
        while (_held.Count < _limit && _waiting.Count > 0)
        {
            var next = _waiting.OrderBy(x => x.Rank).ThenBy(x => x.Seq).First();
            _waiting.Remove(next);
            var lease = new BuildLease(Guid.NewGuid(), next.Holder, DateTimeOffset.UtcNow);
            _held[lease.Id] = lease;
            next.Grant.TrySetResult(lease);
        }
    }
}
