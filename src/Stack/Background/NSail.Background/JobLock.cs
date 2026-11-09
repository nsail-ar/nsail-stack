// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace NSail.Background;

// A Postgres advisory lock on a connection of its own, held for the length of a pass: the job
// shares its scope's DbContext and its transactions, and a lock on that connection would be
// released or returned to the pool under it. Session-level, so a process that dies frees it.
sealed class JobLock : IAsyncDisposable
{
    readonly NpgsqlConnection _connection;
    readonly long _key;

    JobLock(NpgsqlConnection connection, long key)
    {
        _connection = connection;
        _key = key;
    }

    // Null where the lock is held elsewhere. A store that is not Postgres has no such lock to
    // take and is answered as a single instance, which is all it can be.
    public static async Task<JobLock?> TryTake(DbContext? db, string job, string tenant, CancellationToken cancellationToken)
    {
        if (db is null || !db.Database.IsNpgsql() || db.Database.GetConnectionString() is not { } connectionString)
        {
            return NoLock;
        }

        var connection = new NpgsqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var key = KeyOf(job, tenant);

            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);

            command.Parameters.AddWithValue("key", key);

            if ((bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!)
            {
                return new JobLock(connection, key);
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

    // The answer for a store with nothing to lock: held by nobody, released by nothing.
    static JobLock NoLock { get; } = new(null!, 0);

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", _connection);

            command.Parameters.AddWithValue("key", _key);

            await command.ExecuteScalarAsync().ConfigureAwait(false);
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

    // Stable across processes and runtimes, which string.GetHashCode is not.
    static long KeyOf(string job, string tenant)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(job + "\n" + tenant));

        return BitConverter.ToInt64(hash, 0);
    }
}
