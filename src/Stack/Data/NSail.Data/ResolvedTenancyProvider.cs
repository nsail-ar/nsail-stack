// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>The provider a host that resolves tenants gets. The tenant is whoever the edge
/// entered — read from <see cref="AmbientTenant"/>, so every scope the request opens under
/// itself answers the same one — or the one pinned onto this scope for work that outlives the
/// request that started it (a Blazor circuit). With nothing entered it answers exactly as the
/// base does, which under a per-tenant mode is the refusal: a scope that reached a
/// <c>DbContext</c> without a tenant is told rather than quietly served the install's own
/// database.</summary>
public sealed class ResolvedTenancyProvider : TenancyProvider
{
    readonly TenantDatabases? _databases;

    Tenant? _pinned;

    // The registry is optional because only one of the two walls has one: a mode that scopes
    // rows shares the install's single database, so there is no name to look up and nothing to
    // connect to but the install's own string.
    public ResolvedTenancyProvider(
        TenancyOptions tenancy,
        TenantDatabases? databases = null,
        WellKnownRows? rows = null,
        InstallScopedRows? shared = null)
        : base(tenancy, rows, shared)
    {
        _databases = databases;
    }

    public override Tenant Current
    {
        get { return _pinned ?? AmbientTenant.Current; }
    }

    /// <summary>Enters the tenant for the rest of this flow of work, and pins it onto this
    /// scope so work that outlives the flow keeps it.</summary>
    public void Enter(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (_pinned is not null)
        {
            throw new InvalidOperationException(
                $"Tenant '{_pinned.Slug}' is already entered for this scope. A context built on the first one keeps its connection, so a second tenant here would serve two databases under one unit of work.");
        }

        _pinned = tenant;

        AmbientTenant.Enter(tenant);
    }

    public override string ResolveConnection(string installConnectionString)
    {
        if (_databases is null || Current is not { IsResolved: true, Database: { } database })
        {
            return base.ResolveConnection(installConnectionString);
        }

        return _databases.ConnectionFor(database);
    }
}
