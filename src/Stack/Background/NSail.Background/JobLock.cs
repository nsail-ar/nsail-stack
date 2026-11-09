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
    // take and is answered as a single instance, which is all it can be.
    public static async Task<IAsyncDisposable?> TryTake(DbContext? db, string job, string tenant, CancellationToken cancellationToken)
    {
        if (db is null || !db.Database.IsNpgsql() || db.Database.GetConnectionString() is not { } connectionString)
        {
            return NoLock.Instance;
        }

        return await AdvisoryLock.TryTake(connectionString, job + "\n" + tenant, cancellationToken).ConfigureAwait(false);
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
