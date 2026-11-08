// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Messaging.Queue;

/// <summary>What an operator does with this tenant's dead letters: put them back in the queue or
/// delete them. The tenant is whoever the caller entered, through the same query filter the
/// drain relies on.</summary>
public sealed class QueueDeadLetters
{
    readonly DbContext _db;
    readonly TimeProvider _clock;

    public QueueDeadLetters(DbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Makes the given dead letters (all of them when <paramref name="ids"/> is null)
    /// due now with a fresh attempt count; answers how many moved.</summary>
    public async Task<int> Requeue(IReadOnlyCollection<Guid>? ids, CancellationToken cancellationToken)
    {
        var rows = await Select(ids).ToListAsync(cancellationToken).ConfigureAwait(false);
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var row in rows)
        {
            row.DeadLetteredAt = null;
            row.Attempts = 0;
            row.NextAttemptAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return rows.Count;
    }

    /// <summary>Deletes the given dead letters (all of them when <paramref name="ids"/> is
    /// null); answers how many went.</summary>
    public async Task<int> Purge(IReadOnlyCollection<Guid>? ids, CancellationToken cancellationToken)
    {
        var rows = await Select(ids).ToListAsync(cancellationToken).ConfigureAwait(false);

        _db.RemoveRange(rows);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return rows.Count;
    }

    IQueryable<QueuedMessage> Select(IReadOnlyCollection<Guid>? ids)
    {
        var rows = _db.Set<QueuedMessage>().Where(row => row.DeadLetteredAt != null);

        return ids is null ? rows : rows.Where(row => ids.Contains(row.Id));
    }
}
