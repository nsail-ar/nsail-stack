// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Who the install's tenants are. Work that runs outside a request — a background
/// sweep, a startup pass — has no proxy to name one and enters each tenant this answers; a
/// request where the wall is the tenant column asks it whether the slug the proxy sent is one
/// (<see cref="Declares"/>, <see cref="Holds"/>). Registered by the modes that resolve a tenant
/// and by no other, so a host that asks for it and gets nothing is an install whose work runs
/// once, for the install itself.
///
/// <para>Each wall answers from its own truth and neither answer is a guess. Where the wall is
/// the connection, the databases Postgres holds ARE the registry — the same truth a request
/// resolves against, read whole. Where the wall is the tenant column there is no registry to
/// read, so the install <b>declares</b> its own, in <c>Tenancy:Tenants</c> and in the file
/// <c>Tenancy:TenantsFile</c> names — the union of the two, the file read again on every call so
/// that whoever provisions tenants can add one without a restart. Rows cannot answer that
/// question — a query spanning tenants is exactly what the filter refuses — and a roster read
/// off the data would name whoever happens to have written, never whoever exists. An install
/// that declares neither has only the proxy's whitelist, and every well-formed slug is its
/// tenant.</para>
///
/// <para>A named file that cannot be read whole refuses the whole answer rather than returning
/// the part it has: a sweep that silently enters a subset of the install, or a request served
/// for a slug nobody could confirm, is the failure this roster exists to make visible.</para></summary>
public sealed class TenantRoster
{
    // One grammar for both declarations: a file is one slug per line, and a line holding a
    // comma-separated list reads the same as the configuration key does.
    static readonly char[] Separators = [',', ' ', '\t', '\r', '\n'];

    readonly TenantDatabases? _databases;
    readonly TenancyOptions _tenancy;
    readonly IReadOnlyList<Tenant> _declared;

    // The registry is optional for the reason ResolvedTenancyProvider's is: only one of the two
    // walls has one.
    public TenantRoster(TenancyOptions tenancy, TenantDatabases? databases = null)
    {
        ArgumentNullException.ThrowIfNull(tenancy);

        _databases = databases;
        _tenancy = tenancy;

        // Read at construction rather than on the first sweep, so a cell that spells a slug it
        // cannot name is refused at startup — where a badly named product or cell is refused.
        // The file is not: a cell boots before whoever provisions its tenants has written it.
        _declared = databases is null ? Named(tenancy.Tenants, "Tenancy:Tenants") : [];
    }

    /// <summary>Whether this install states its tenants where the wall is the tenant column: a
    /// <c>Tenancy:Tenants</c> that names one, or a <c>Tenancy:TenantsFile</c> at all — a file that
    /// is named and empty declares nobody. Never true where the wall is the connection, whose
    /// registry a request already resolves against.</summary>
    public bool Declares
    {
        get
        {
            return _databases is null && (_declared.Count > 0 || !string.IsNullOrWhiteSpace(_tenancy.TenantsFile));
        }
    }

    public async Task<bool> Holds(Tenant tenant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var tenants = await Tenants(cancellationToken).ConfigureAwait(false);

        return tenants.Any(declared => string.Equals(declared.Slug, tenant.Slug, StringComparison.Ordinal));
    }

    public async Task<IReadOnlyList<Tenant>> Tenants(CancellationToken cancellationToken = default)
    {
        if (_databases is not null)
        {
            return await _databases.All(cancellationToken).ConfigureAwait(false);
        }

        var file = _tenancy.TenantsFile;

        if (string.IsNullOrWhiteSpace(file))
        {
            return _declared;
        }

        return Union(_declared, Named(await Read(file, cancellationToken).ConfigureAwait(false), $"Tenancy:TenantsFile ('{file}')"));
    }

    static async Task<string> Read(string file, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Tenancy:TenantsFile names '{file}', which could not be read, so the roster is unknown. The file is the install's declaration of its tenants, one slug per line, written by whoever provisions them.",
                exception);
        }
    }

    static IReadOnlyList<Tenant> Named(string? declaration, string source)
    {
        var slugs = (declaration ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return slugs
            .Select(slug => TenantSlug.Normalize(slug)
                ?? throw new InvalidOperationException(
                    $"{source} names '{slug}', which cannot be a tenant. The roster is the slugs the proxy sends, in lowercase alphanumerics and hyphens, one per line or separated by commas."))
            .Distinct(StringComparer.Ordinal)
            .Select(Tenant.For)
            .ToList();
    }

    static IReadOnlyList<Tenant> Union(IReadOnlyList<Tenant> declared, IReadOnlyList<Tenant> filed)
    {
        if (declared.Count == 0)
        {
            return filed;
        }

        return declared
            .Concat(filed)
            .DistinctBy(tenant => tenant.Slug, StringComparer.Ordinal)
            .ToList();
    }
}
