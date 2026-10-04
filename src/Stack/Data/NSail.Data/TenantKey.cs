// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

// A tenant's key in a shared table is DERIVED from its slug, never stored and never allocated.
// Under a per-tenant connection the registry is pg_database (data-tenancy.md); a shared database has no
// equivalent truth to read, and a catalog table would be the third runtime dependency the mode
// was designed without. Derivation answers instead: the same slug is the same key in every
// process, every replica and every restart, so nothing has to be looked up before a query can
// be filtered — and the proxy stays the whitelist, exactly as it is for the other wall.
public static class TenantKey
{
    // Name-based, so the mapping is a function of the slug and of this seam alone. The prefix
    // is what keeps the key from being any other hash of the same word.
    const string Namespace = "nsail:tenant:";

    /// <summary>The id of one of the rows a tenant is born with (<see cref="ITenantSeed"/>),
    /// derived from the tenant and a name the seed gives it — its organization, its
    /// administrator, an account in its chart. Derived rather than drawn for the same reason
    /// the tenant's own key is: two processes planting one tenant write one set of rows, and
    /// the same slug means the same rows after any restart.</summary>
    public static Guid For(string slug, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return For($"{slug}/{name}");
    }

    /// <summary>The tenant's own twin of a row the install freezes by id — a seeded Role, an
    /// account <c>AccountingSettings</c> names. Derived from that very id rather than from a name
    /// of its own so that the seam translating a frozen id
    /// (<see cref="TenancyProvider.Row(Guid)"/>) and the bootstrap planting the row are one
    /// function called from two places, never two that agree today.</summary>
    public static Guid For(string slug, Guid wellKnown)
    {
        return For(slug, wellKnown.ToString("D"));
    }

    public static Guid For(string slug)
    {
        return DerivedKey.For(Namespace + slug);
    }
}
