// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NSail.Data;

// The write half of the wall, and it sits where the unit of work already sits rather than in a
// handler: every save in the process passes here, and a rule a handler has to remember is a rule
// that is one new handler away from being forgotten.
//
// The tenant comes from the CONTEXT — the same answer the query filter reads, so a row is never
// written under one tenant and read under another. Nothing a caller sent is consulted, and there
// is nothing it could have sent: the column is a shadow property, so no message, no entity and
// no payload can name it. That is the difference between a wall and a suggestion.
sealed class TenantStamp : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Stamp(eventData.Context);

        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Stamp(eventData.Context);

        return ValueTask.FromResult(result);
    }

    static void Stamp(DbContext? context)
    {
        if (context is not ITenanted tenanted)
        {
            return;
        }

        var tenant = TenantColumn.Of(tenanted);

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added || entry.Metadata.FindProperty(TenantColumn.Name) is null)
            {
                continue;
            }

            // The read half answers "nothing" by comparing against NULL; the write half cannot,
            // because a row has to carry some value and every value here would be somebody's.
            // Stamping the install's key would hide the row from every tenant AND show it to
            // every unresolved scope, so the save is refused instead — loudly, at the seam,
            // rather than as a row nobody ever reads back.
            if (tenant is not { } key)
            {
                throw new InvalidOperationException(
                    $"Saving a new {entry.Metadata.DisplayName()} under a scope that resolved no tenant, in a mode whose rows belong to one. Work that runs outside a request says whose it is: a background job declares IBackgroundJob.Tenancy = Tenant and the runner gives it one pass per tenant on the roster, each with that tenant entered (ResolvedTenancyProvider.Enter). Work that is the install's own touches no tenant-scoped entity.");
            }

            entry.Property(TenantColumn.Name).CurrentValue = key;
        }
    }
}
