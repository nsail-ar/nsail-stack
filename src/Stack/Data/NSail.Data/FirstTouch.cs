// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using Npgsql;

namespace NSail.Data;

/// <summary>Work a tenant pays for exactly once, on the first request that needs it: applying
/// the chain to a database provisioning only created, planting the rows a shared database's
/// tenant is born with. Shared by both walls because the discipline is the same one — a lock
/// that crosses processes, and a memo that keeps the cost off every later request.</summary>
sealed class FirstTouch
{
    // What this process has already done. The lock below is what makes the answer safe across
    // processes; this only keeps the cost off every later request.
    readonly ConcurrentDictionary<string, bool> _done = new(StringComparer.Ordinal);

    /// <summary>Runs <paramref name="work"/> at most once per <paramref name="key"/>, and never
    /// twice at the same moment anywhere: the lock is a Postgres advisory lock, so it crosses
    /// processes and machines with no infrastructure behind it. Two first touches produce one
    /// run and one wait.</summary>
    public async Task Once(
        string connectionString,
        string key,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(work);

        if (_done.ContainsKey(key))
        {
            return;
        }

        // A session-level advisory lock belongs to the connection that took it, so it gets one
        // of its own: the connection the work itself runs on is EF's, pooled and reset
        // underneath it.
        await using var gate = new NpgsqlConnection(connectionString);

        await gate.OpenAsync(cancellationToken);

        var lockKey = LockKey(key);

        await Advisory(gate, "pg_advisory_lock", lockKey, cancellationToken);

        try
        {
            await work(cancellationToken);
        }
        finally
        {
            await Advisory(gate, "pg_advisory_unlock", lockKey, CancellationToken.None);
        }

        _done[key] = true;
    }

    static async Task Advisory(NpgsqlConnection connection, string function, long key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT {function}(@key)", connection);

        command.Parameters.AddWithValue("key", key);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // FNV-1a, not string.GetHashCode: .NET randomizes that one per process, and the whole
    // point of this key is that two processes touching one tenant ask for the same lock.
    static long LockKey(string key)
    {
        var hash = 14695981039346656037UL;

        foreach (var character in key)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }

        return unchecked((long)hash);
    }
}
