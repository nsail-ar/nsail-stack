// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data;

/// <summary>Reads what the install records about the tenant this scope is in
/// (<see cref="TenantState"/>) — the edge asks before it serves a request, the background runner
/// before it sweeps a tenant. Registered by the modes that resolve a tenant and by no other: an
/// install with no tenants has no state to read and nothing to refuse.
///
/// <para><b>The row is memoed for minutes; the clock is read every time.</b> The lookup is held
/// per tenant by <see cref="TenantStateMemo"/> — every request passed through here, and almost
/// every one of them read the same absent row. Whether a memoed row's window is open is still
/// asked of the clock on each request, so a scheduled pause lands to the second. What the memo
/// costs is stated rather than hidden: the row is written by a program outside this process, so
/// a suspension written now reaches a running install when its entry expires, and so does a
/// restoration (data-tenancy.md, Tenant state).</para></summary>
public sealed class TenantStates
{
    readonly DbContext _db;
    readonly TenancyProvider _tenancy;
    readonly TenantStateMemo _memo;
    readonly TimeProvider _clock;

    public TenantStates(DbContext db, TenancyProvider tenancy, TenantStateMemo memo, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenancy);
        ArgumentNullException.ThrowIfNull(memo);
        ArgumentNullException.ThrowIfNull(clock);

        _db = db;
        _tenancy = tenancy;
        _memo = memo;
        _clock = clock;
    }

    /// <summary>The row for the tenant this scope is in, or null where there is none — which is
    /// every tenant nobody has acted on, and every scope that resolved no tenant at all. The
    /// instance is shared with every other scope that read it inside the memo's window, so it is
    /// read and never written.</summary>
    public async Task<TenantState?> Current(CancellationToken cancellationToken = default)
    {
        if (_tenancy.RowKey is not { } key)
        {
            return null;
        }

        return await _memo.Of(key, token => Read(key, token), cancellationToken);
    }

    /// <summary>The row when this scope's tenant is paused at this moment — the open-ended switch
    /// or a window that has opened and not closed — and null when it is not. A tenant with no
    /// row, a word this Stack does not know, a window that is not open yet or is already over,
    /// and a scope in no tenant all answer null: the gate refuses what is positively suspended
    /// and nothing else, and what comes back is what its page words itself from.</summary>
    public async Task<TenantState?> Suspension(CancellationToken cancellationToken = default)
    {
        if (await Current(cancellationToken) is not { } state)
        {
            return null;
        }

        return state.IsSuspendedAt(_clock.GetUtcNow().UtcDateTime) ? state : null;
    }

    /// <summary>Whether this scope's tenant is suspended — what work that only has to stop asks
    /// of <see cref="Suspension"/>.</summary>
    public async Task<bool> IsSuspended(CancellationToken cancellationToken = default)
    {
        return await Suspension(cancellationToken) is not null;
    }

    async Task<TenantState?> Read(Guid key, CancellationToken cancellationToken)
    {
        return await _db.Set<TenantState>()
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == key, cancellationToken);
    }
}
