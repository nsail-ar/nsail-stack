// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// The turns for local builds, a machine's second scarce resource after the AI's requests. A lane
// holds an agent slot for its whole voyage but a build turn only while a `dotnet` compiles (a
// run's or a watch's build, not its app), so the CPU is rationed where it is spent. When a turn frees, it goes to the WAITER OF
// HIGHEST PRIORITY (the lowest rank, the planner's order), never to the one that asked first.
// A release's suite holds its turn alone against the gates: both draw on the machine's one
// Postgres, and a gate's clock starts at its turn. No gate is granted while a suite holds or awaits
// a turn, and a suite waits only for the turns gates already hold. Lanes are never held back.
public sealed class BuildLeases(int limit)
{

    sealed record Waiter(int Rank, long Seq, string Holder, BuildTurn Turn, TaskCompletionSource<BuildLease> Grant);

    readonly Lock _gate = new();
    readonly List<Waiter> _waiting = [];
    readonly Dictionary<Guid, (BuildLease Lease, BuildTurn Turn)> _held = [];
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
        lock (_gate) return _held.Values.Select(h => h.Lease).OrderBy(l => l.At).ToList();
    }

    public Task<BuildLease> Acquire(string holder, int rank, CancellationToken ct)
    {
        return Acquire(holder, rank, BuildTurn.Lane, ct);
    }

    // A lower rank is more urgent. Cancelling the wait gives up the place in line.
    public async Task<BuildLease> Acquire(string holder, int rank, BuildTurn turn, CancellationToken ct)
    {
        Waiter w;
        lock (_gate)
        {
            w = new Waiter(rank, _seq++, holder, turn, new TaskCompletionSource<BuildLease>(TaskCreationOptions.RunContinuationsAsynchronously));
            _waiting.Add(w);
            Grant();
        }

        await using var reg = ct.Register(() =>
        {
            lock (_gate)
            {
                if (_waiting.Remove(w))
                {
                    w.Grant.TrySetCanceled(ct);
                    Grant();
                }
            }
        });
        return await w.Grant.Task;
    }

    // A lane killed mid-build never gives its turn back: no job outlasts its step's minutes, so a
    // turn older than the longest one is dead and goes back to the line.
    public int Sweep(TimeSpan olderThan, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        lock (_gate)
        {
            var stale = _held.Values.Where(h => at - h.Lease.At > olderThan).Select(h => h.Lease.Id).ToList();
            foreach (var id in stale) _held.Remove(id);
            if (stale.Count > 0) Grant();
            return stale.Count;
        }
    }

    public int ReleaseHolder(string holder)
    {
        lock (_gate)
        {
            var gone = _held.Values.Where(h => h.Lease.Holder == holder).Select(h => h.Lease.Id).ToList();
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
        while (_held.Count < _limit)
        {
            var next = _waiting.OrderBy(x => x.Rank).ThenBy(x => x.Seq).FirstOrDefault(Grantable);
            if (next is null) return;
            _waiting.Remove(next);
            var lease = new BuildLease(Guid.NewGuid(), next.Holder, DateTimeOffset.UtcNow);
            _held[lease.Id] = (lease, next.Turn);
            next.Grant.TrySetResult(lease);
        }
    }

    bool Grantable(Waiter w)
    {
        return w.Turn switch
        {
            BuildTurn.Gate => !_held.Values.Any(h => h.Turn == BuildTurn.Suite) && !_waiting.Any(x => x.Turn == BuildTurn.Suite),
            BuildTurn.Suite => !_held.Values.Any(h => h.Turn == BuildTurn.Gate),
            _ => true,
        };
    }
}

// What a build turn is for: a release's suite is held alone against the gates (BuildLeases).
public enum BuildTurn
{
    Lane,
    Gate,
    Suite,
}
