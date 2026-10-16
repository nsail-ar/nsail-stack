// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;

namespace NSail.Data;

/// <summary>What this process has read about its tenants lately (<see cref="TenantState"/>), so
/// the gate every request passes costs one lookup per tenant per window instead of one per
/// request. The window is <see cref="TenancyOptions.StateCacheSeconds"/>; a tenant with no row
/// is remembered too, because that is the answer almost every request gets and the one it would
/// be absurd to pay for.
///
/// <para>The cost is stated rather than hidden: the row is written from OUTSIDE this process, so
/// a change reaches a running install only when its entry expires — suspending for abuse is not
/// instant, and neither is restoring (data-tenancy.md, Tenant state).</para></summary>
public sealed class TenantStateMemo
{
    readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    readonly TimeProvider _clock;
    readonly TimeSpan _life;

    public TenantStateMemo(TenancyOptions tenancy, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(tenancy);
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
        _life = TimeSpan.FromSeconds(tenancy.StateCacheSeconds);
    }

    /// <summary>The state of <paramref name="tenant"/>, read through <paramref name="read"/> only
    /// when nothing unexpired is held for it. The instance handed back is shared by every caller
    /// inside the window and must be treated as read-only — nothing in the app writes this row.</summary>
    public async Task<TenantState?> Of(
        Guid tenant,
        Func<CancellationToken, Task<TenantState?>> read,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        var now = _clock.GetUtcNow();

        if (_entries.TryGetValue(tenant, out var held) && now < held.Until)
        {
            return held.State;
        }

        var state = await read(cancellationToken);

        // Last writer wins, and no lock: two requests racing the same expiry make two lookups
        // and agree on the answer. A gate that served a stampede by making them wait on each
        // other would be the cost this memo exists to remove.
        _entries[tenant] = new Entry(state, now + _life);

        return state;
    }

    readonly record struct Entry(TenantState? State, DateTimeOffset Until);
}
