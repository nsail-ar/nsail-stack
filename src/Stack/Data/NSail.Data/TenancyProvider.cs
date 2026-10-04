// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>The tenancy seam's one injected service: who the current tenant is, and which
/// connection the work runs on. The default answers <see cref="Tenant.None"/> and hands
/// back the install's own connection, so an install that states nothing behaves as it did
/// before the seam existed. A host that resolves tenants replaces it — the connection is
/// resolved here rather than at the call site so no product writes tenancy code.</summary>
public class TenancyProvider
{
    /// <summary>The answer for a context nobody composed through a host: a design-time
    /// migration, a fixture building its own context. It resolves no tenant, which is what an
    /// install under <c>None</c> resolves too.</summary>
    public static TenancyProvider Install { get; } = new(new TenancyOptions());

    readonly TenancyOptions _tenancy;
    readonly WellKnownRows _rows;
    readonly InstallScopedRows _shared;

    public TenancyProvider(TenancyOptions tenancy, WellKnownRows? rows = null, InstallScopedRows? shared = null)
    {
        ArgumentNullException.ThrowIfNull(tenancy);

        _tenancy = tenancy;
        _rows = rows ?? WellKnownRows.None;
        _shared = shared ?? InstallScopedRows.None;
    }

    public virtual Tenant Current
    {
        get { return Tenant.None; }
    }

    /// <summary>Which tenant's rows this scope reads and writes, and <c>null</c> where it must
    /// touch none. Wherever the wall is not the column — <c>None</c> has no tenants, <c>MultiDb</c>
    /// has the connection — the answer is <see cref="Guid.Empty"/>, the install's own: every row
    /// carries it and the filter comparing against it matches everything, which is how the model
    /// stays identical in every mode without the mode changing behaviour.
    ///
    /// <para>Where the wall IS the column, a scope that resolved no tenant answers null instead
    /// of the install's key, and null is not a convenience: SQL's <c>= NULL</c> is unknown, so
    /// the filter matches no row — including the seed's unstamped ones. A background job that
    /// entered no tenant reads NOTHING rather than every tenant's rows, and the constant-true
    /// fallback that would have been the leak wearing a convenience costume never exists. Work
    /// that must touch a tenant's rows enters one (<see cref="ResolvedTenancyProvider.Enter"/>).</para></summary>
    public Guid? RowKey
    {
        get
        {
            if (_tenancy.Rows != TenancyScope.Tenant)
            {
                return Tenant.None.RowKey;
            }

            var tenant = Current;

            return tenant.IsResolved ? tenant.RowKey : null;
        }
    }

    /// <summary>The id, IN THIS SCOPE, of a row the install freezes and code names by constant —
    /// a seeded Role, the account a setting defaults to. Where the rows are the install's the
    /// answer is the constant itself; where they are the tenant's, the tenant owns a twin of that
    /// row and this is its id. The frozen id stays the row's NAME, so a screen, a deep link and a
    /// stored setting keep sending exactly what they sent before and the boundary resolves.
    ///
    /// <para>The mapping is total and mode-invariant: identity under every mode whose rows are the
    /// install's, <see cref="TenantKey.For(string, Guid)"/> under one whose rows are a tenant's,
    /// and an id outside the declared set (<see cref="WellKnownRows"/>) untouched in either — a
    /// stored setting that already names the tenant's own row must not be translated twice. A
    /// scope that resolved no tenant answers the frozen id, which under that mode is a row the
    /// filter hides: reading nothing, as everywhere else in this seam.</para></summary>
    public Guid Row(Guid wellKnown)
    {
        return _rows.Contains(wellKnown) ? Mine(wellKnown) : wellKnown;
    }

    /// <summary>The id, IN THIS SCOPE, of a row a DOCUMENT names — a starter pack is a list of
    /// message invocations shipped as data, so it is a caller like any other, and its guids are
    /// subject to the same wall as a handler's. A guid in such a document is one of three things
    /// and the seam needs to tell only one of them apart: a row the install shares
    /// (<see cref="IInstallScoped"/>, declared) is answered by itself, because there is one of it
    /// and it is everybody's; everything else — a row the tree freezes and the first touch copies,
    /// and the document's own creations and its references to them — is a row of the scope
    /// importing it and is derived from the tenant.
    ///
    /// <para>Deriving what a document creates is what lets the SAME document be imported by every
    /// tenant of one database: a primary key carries no tenant, so a verbatim id is one row in the
    /// whole install and the second tenant to import collides on it. And because the derivation is
    /// a function of the id alone, both ends of a reference inside the document derive to the same
    /// value without the loader ever knowing which guid was the creation. Mode-invariant like
    /// <see cref="Row(Guid)"/>, and the identity wherever the rows are the install's.</para></summary>
    public Guid Owned(Guid id)
    {
        return _shared.Contains(id) ? id : Mine(id);
    }

    Guid Mine(Guid frozen)
    {
        if (_tenancy.Rows != TenancyScope.Tenant)
        {
            return frozen;
        }

        return Current is { IsResolved: true, Slug: { } slug } ? TenantKey.For(slug, frozen) : frozen;
    }

    public virtual string ResolveConnection(string installConnectionString)
    {
        // The other half of the refusal AddDataAccess makes on the Rows switch, made here because
        // this is the switch a provider reads. Serving the install's database to a mode that
        // asked for the tenant's is the leak the mode was configured to prevent, and no slice
        // has replaced this provider yet — a host that resolves tenants overrides this and the
        // refusal goes with it.
        if (_tenancy.Connection == TenancyScope.Tenant)
        {
            throw new InvalidOperationException(
                $"Tenancy:Mode is {_tenancy.Mode}, which connects per tenant, but nothing resolves one. No slice of nsail#355 implements connection resolution, so this install would serve every tenant the install's own database.");
        }

        return ConnectionStrings.Complete(installConnectionString);
    }
}
