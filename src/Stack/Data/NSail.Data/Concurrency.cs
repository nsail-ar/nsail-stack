// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NSail.Problems;

namespace NSail.Data;

static class Concurrency
{
    // The tree's own spelling of the audit timestamp. It is looked up by name rather than by a
    // second interface because an entity opts into being versioned, not into being narrated:
    // the token is what refuses the save, and a row with nothing to say about when it changed
    // still refuses it. Nothing in the model records a "who" yet — when a column does, it joins
    // here and the refusal starts naming a person too.
    const string ModifiedAt = "UpdatedAt";

    public static async Task<Problem> Describe(
        DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken)
    {
        var entry = exception.Entries.Count > 0 ? exception.Entries[0] : null;

        if (entry is null)
        {
            return BusinessProblem.Conflict("Common.Resource");
        }

        return BusinessProblem.Conflict(
            entry.Metadata.ClrType,
            await ReadModifiedAt(entry, cancellationToken).ConfigureAwait(false));
    }

    // Read inside the send's own transaction, which is still usable: a version mismatch is not
    // a database error — the UPDATE ran and matched no row — so the row the other writer
    // committed is there to be asked when it happened.
    static async Task<DateTime?> ReadModifiedAt(EntityEntry entry, CancellationToken cancellationToken)
    {
        var property = entry.Metadata.FindProperty(ModifiedAt);

        if (property is null || property.ClrType != typeof(DateTime))
        {
            return null;
        }

        var stored = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);

        return stored?[property] as DateTime?;
    }
}
