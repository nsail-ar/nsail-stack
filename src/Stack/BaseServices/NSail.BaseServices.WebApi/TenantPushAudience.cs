// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;
using NSail.Messaging.WebApi.Push;

namespace NSail.BaseServices.WebApi;

/// <summary>The push's audience is the tenant this scope resolved: the publishing request's,
/// or the connect request's — TenancyMiddleware, then TenantClaimMiddleware refusing a ticket
/// minted for another, both ahead of the hub.</summary>
public sealed class TenantPushAudience : PushAudience
{
    readonly TenancyProvider _tenancy;
    readonly IServiceScopeFactory? _scopes;
    readonly TenantStateMemo? _memo;
    readonly TimeProvider _clock;

    // Optional because AddDataAccess is what registers the seam, and a host with no store
    // composes none: its clients are the install's one audience, which is what Tenant.None is
    // everywhere else.
    public TenantPushAudience(
        TenancyProvider? tenancy = null,
        IServiceScopeFactory? scopes = null,
        TenantStateMemo? memo = null,
        TimeProvider? clock = null)
    {
        _tenancy = tenancy ?? TenancyProvider.Install;
        _scopes = scopes;
        _memo = memo;
        _clock = clock ?? TimeProvider.System;
    }

    public override string Current
    {
        get { return "tenant:" + (_tenancy.Current.Slug ?? string.Empty); }
    }

    // A feed is held for as long as the tab is open, so a suspension written after it connected
    // reaches it here, from the same memoed state the request wall reads: the feed stops within
    // the state window, plus the second the watch polls at. While the memo holds the tenant's
    // row the ask reads nothing; only once its window has expired does it open a scope in the
    // tenant the feed was opened for and read the row again.
    public override Func<CancellationToken, Task<bool>>? Watch()
    {
        if (_scopes is not { } scopes
            || _tenancy.Current is not { IsResolved: true } tenant
            || _tenancy.StateKey is not { } key)
        {
            return null;
        }

        return async cancellationToken =>
        {
            if (_memo is { } memo && memo.Holds(key, out var held))
            {
                return held is null || !held.IsSuspendedAt(_clock.GetUtcNow().UtcDateTime);
            }

            using var scope = scopes.CreateScope();

            if (scope.ServiceProvider.GetService<ResolvedTenancyProvider>() is not { } tenancy)
            {
                return true;
            }

            // Entered before the state is resolved, as the runner does: the context it reads
            // through takes its connection from the tenant this scope is in.
            tenancy.Enter(tenant);

            if (scope.ServiceProvider.GetService<TenantStates>() is not { } states)
            {
                return true;
            }

            return !await states.IsSuspended(cancellationToken);
        };
    }
}
