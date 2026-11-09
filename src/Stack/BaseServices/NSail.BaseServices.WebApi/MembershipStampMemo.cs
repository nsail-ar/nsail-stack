// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using NSail.Data;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

/// <summary>What this process has read about each party's memberships lately
/// (<see cref="CredentialProvider.MembershipStamp"/>), so the stale-ticket check costs one read
/// per party per <see cref="TenancyOptions.StateCacheSeconds"/> instead of one per request. The
/// rows change from outside this process, so a revocation reaches a ticket when its entry
/// expires.</summary>
public sealed class MembershipStampMemo
{
    readonly ConcurrentDictionary<(string?, Guid), Entry> _entries = new();
    readonly TimeProvider _clock;
    readonly TimeSpan _life;

    public MembershipStampMemo(TenancyOptions tenancy, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(tenancy);
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
        _life = TimeSpan.FromSeconds(tenancy.StateCacheSeconds);
    }

    public async Task<string?> Of(
        string? tenant,
        Guid party,
        Func<CancellationToken, Task<string?>> read,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        var now = _clock.GetUtcNow();

        if (_entries.TryGetValue((tenant, party), out var held) && now < held.Until)
        {
            return held.Stamp;
        }

        var stamp = await read(cancellationToken);

        // Last writer wins, no lock: a stampede on one expiry makes a few reads that agree.
        _entries[(tenant, party)] = new Entry(stamp, now + _life);

        return stamp;
    }

    readonly record struct Entry(string? Stamp, DateTimeOffset Until);
}
