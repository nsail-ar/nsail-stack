// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Data;
using NSail.Messaging.WebApi.Push;

namespace NSail.BaseServices.WebApi;

/// <summary>The push's audience is the tenant this scope resolved: the publishing request's,
/// or the connect request's — TenancyMiddleware, then TenantClaimMiddleware refusing a ticket
/// minted for another, both ahead of the hub.</summary>
public sealed class TenantPushAudience : PushAudience
{
    readonly TenancyProvider _tenancy;

    // Resolved rather than required: a host with no store composes no tenancy seam, and its
    // clients are the install's one audience, which is what Tenant.None is everywhere else.
    public TenantPushAudience(IServiceProvider services)
    {
        _tenancy = services.GetService<TenancyProvider>() ?? TenancyProvider.Install;
    }

    public override string Current
    {
        get { return "tenant:" + (_tenancy.Current.Slug ?? string.Empty); }
    }
}
