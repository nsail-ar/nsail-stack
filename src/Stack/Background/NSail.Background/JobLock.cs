// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data;

namespace NSail.Background;

// The advisory lock a pass holds, on the job's store. The job shares its scope's DbContext and
// its transactions, so the lock rides a connection of its own (AdvisoryLock).
static class JobLock
{
    // Null where the lock is held elsewhere. A store that is not Postgres has no such lock to
    // take and is answered as a single instance, which is all it can be. The key names the job
    // by its full name: two kits' jobs of one simple name are not siblings.
    public static async Task<IAsyncDisposable?> TryTake(DbContext? db, Type job, string tenant, CancellationToken cancellationToken)
    {
        if (db is null || !db.Database.IsNpgsql() || db.Database.GetConnectionString() is not { } connectionString)
        {
            return NoLock.Instance;
        }

        return await AdvisoryLock.TryTake(connectionString, Name(job) + "\n" + tenant, cancellationToken).ConfigureAwait(false);
    }

    // Install-wide work under a per-tenant connection: the DbContext cannot be asked for, so the
    // lock sits on the maintenance database the roster is read from. That database is the
    // cluster's, so the key carries the cell's prefix and the job's full name.
    public static async Task<IAsyncDisposable?> TryTakeOnCluster(TenantDatabases? databases, Type job, CancellationToken cancellationToken)
    {
        if (databases is null)
        {
            return NoLock.Instance;
        }

        return await AdvisoryLock.TryTake(databases.MaintenanceConnection, databases.CellPrefix + Name(job), cancellationToken).ConfigureAwait(false);
    }

    static string Name(Type job)
    {
        return job.FullName ?? job.Name;
    }

    public sealed class NoLock : IAsyncDisposable
    {
        public static NoLock Instance { get; } = new();

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
