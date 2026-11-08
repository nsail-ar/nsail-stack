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

    // Optional because AddDataAccess is what registers the seam, and a host with no store
    // composes none: its clients are the install's one audience, which is what Tenant.None is
    // everywhere else.
    public TenantPushAudience(TenancyProvider? tenancy = null, IServiceScopeFactory? scopes = null)
    {
        _tenancy = tenancy ?? TenancyProvider.Install;
        _scopes = scopes;
    }

    public override string Current
    {
        get { return "tenant:" + (_tenancy.Current.Slug ?? string.Empty); }
    }

    // A feed is held for as long as the tab is open, so a suspension written after it connected
    // reaches it here: each ask opens a scope of its own in the tenant the feed was opened for,
    // and reads the same memoed state the request wall does — the feed stops within the state
    // window, plus the second the watch polls at.
    public override Func<CancellationToken, Task<bool>>? Watch()
    {
        if (_scopes is not { } scopes || _tenancy.Current is not { IsResolved: true } tenant)
        {
            return null;
        }

        return async cancellationToken =>
        {
            using var scope = scopes.CreateScope();

            if (scope.ServiceProvider.GetService<ResolvedTenancyProvider>() is not { } tenancy
                || scope.ServiceProvider.GetService<TenantStates>() is not { } states)
            {
                return true;
            }

            tenancy.Enter(tenant);

            return !await states.IsSuspended(cancellationToken);
        };
    }
}
