// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;

namespace NSail.Data;

/// <summary>A session-level Postgres advisory lock on a connection of its own, held until
/// disposed. It crosses processes and machines with no infrastructure behind it, and a process
/// that dies frees it. The connection is its own because the lock belongs to the session that
/// took it, and the one the work runs on is EF's, pooled and reset underneath it.</summary>
public sealed class AdvisoryLock : IAsyncDisposable
{
    readonly NpgsqlConnection _connection;
    readonly long _key;

    AdvisoryLock(NpgsqlConnection connection, long key)
    {
        _connection = connection;
        _key = key;
    }

    /// <summary>Waits for the lock on <paramref name="key"/>.</summary>
    public static async Task<AdvisoryLock> Take(string connectionString, string key, CancellationToken cancellationToken = default)
    {
        return (await Acquire(connectionString, key, "pg_advisory_lock", cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>Takes the lock on <paramref name="key"/>, or answers null where another session
    /// holds it.</summary>
    public static Task<AdvisoryLock?> TryTake(string connectionString, string key, CancellationToken cancellationToken = default)
    {
        return Acquire(connectionString, key, "pg_try_advisory_lock", cancellationToken);
    }

    static async Task<AdvisoryLock?> Acquire(string connectionString, string key, string function, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var connection = new NpgsqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var lockKey = KeyOf(key);

            await using var command = new NpgsqlCommand($"SELECT {function}(@key)", connection);

            command.Parameters.AddWithValue("key", lockKey);

            // pg_advisory_lock answers void, which reads as DBNull: it returns only once held.
            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not false)
            {
                return new AdvisoryLock(connection, lockKey);
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);

            throw;
        }

        await connection.DisposeAsync().ConfigureAwait(false);

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", _connection);

            command.Parameters.AddWithValue("key", _key);

            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The session ends with the connection, and ending it is what frees the lock.
        }
        finally
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    // FNV-1a, not string.GetHashCode: .NET randomizes that one per process, and the whole
    // point of this key is that two processes asking for one thing ask for the same lock.
    static long KeyOf(string key)
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
