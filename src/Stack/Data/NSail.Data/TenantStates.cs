// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data;

/// <summary>Reads what the install records about the tenant this scope is in
/// (<see cref="TenantState"/>) — the edge asks before it serves a request, the background runner
/// before it sweeps a tenant. Registered by the modes that resolve a tenant and by no other: an
/// install with no tenants has no state to read and nothing to refuse.
///
/// <para><b>Nothing is cached, and that is the design.</b> The row is written by a program
/// outside this process, so a memo would serve a state the install has already changed: a tenant
/// suspended for abuse would keep being served, and a reactivated one would stay dead, for
/// however long the window was. What it costs instead is one primary-key lookup per request,
/// beside the one the credential already buys (data-tenancy.md, Tenant state).</para></summary>
public sealed class TenantStates
{
    readonly DbContext _db;
    readonly TenancyProvider _tenancy;

    public TenantStates(DbContext db, TenancyProvider tenancy)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenancy);

        _db = db;
        _tenancy = tenancy;
    }

    /// <summary>The row for the tenant this scope is in, or null where there is none — which is
    /// every tenant nobody has acted on, and every scope that resolved no tenant at all.</summary>
    public async Task<TenantState?> Current(CancellationToken cancellationToken = default)
    {
        if (_tenancy.RowKey is not { } key)
        {
            return null;
        }

        return await _db.Set<TenantState>()
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == key, cancellationToken);
    }

    /// <summary>Whether this scope's tenant is suspended. A tenant with no row, a word this
    /// Stack does not know and a scope in no tenant all answer false: the gate refuses what is
    /// positively suspended and nothing else.</summary>
    public async Task<bool> IsSuspended(CancellationToken cancellationToken = default)
    {
        return await Current(cancellationToken) is { IsSuspended: true };
    }
}
