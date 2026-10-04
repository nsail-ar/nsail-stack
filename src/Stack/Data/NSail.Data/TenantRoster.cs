// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Who the install's tenants are, for work that runs outside a request and therefore
/// has no proxy to name one — a background sweep, a startup pass. Registered by the modes that
/// resolve a tenant and by no other, so a host that asks for it and gets nothing is an install
/// whose work runs once, for the install itself.
///
/// <para>Each wall answers from its own truth and neither answer is a guess. Where the wall is
/// the connection, the databases Postgres holds ARE the registry — the same truth a request
/// resolves against, read whole. Where the wall is the tenant column there is no registry to
/// read: the proxy's whitelist is the roster and a well-formed slug IS the tenant, so the
/// install <b>declares</b> its own in <c>Tenancy:Tenants</c>. Rows cannot answer that question —
/// a query spanning tenants is exactly what the filter refuses — and a roster read off the data
/// would name whoever happens to have written, never whoever exists.</para></summary>
public sealed class TenantRoster
{
    readonly TenantDatabases? _databases;
    readonly IReadOnlyList<Tenant> _declared;

    // The registry is optional for the reason ResolvedTenancyProvider's is: only one of the two
    // walls has one.
    public TenantRoster(TenancyOptions tenancy, TenantDatabases? databases = null)
    {
        ArgumentNullException.ThrowIfNull(tenancy);

        _databases = databases;

        // Read at construction rather than on the first sweep, so a cell that spells a slug it
        // cannot name is refused at startup — where a badly named product or cell is refused.
        _declared = databases is null ? Declared(tenancy) : [];
    }

    public Task<IReadOnlyList<Tenant>> Tenants(CancellationToken cancellationToken = default)
    {
        return _databases is null ? Task.FromResult(_declared) : _databases.All(cancellationToken);
    }

    static IReadOnlyList<Tenant> Declared(TenancyOptions tenancy)
    {
        var slugs = (tenancy.Tenants ?? string.Empty).Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return slugs
            .Select(slug => TenantSlug.Normalize(slug)
                ?? throw new InvalidOperationException(
                    $"Tenancy:Tenants names '{slug}', which cannot be a tenant. The roster is the slugs the proxy sends, in lowercase alphanumerics and hyphens, separated by commas."))
            .Distinct(StringComparer.Ordinal)
            .Select(Tenant.For)
            .ToList();
    }
}
