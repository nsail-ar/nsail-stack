// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Which branches the work in hand can see: the subtree of the organization the
/// operation is about — the one its message names, else the session's — walked once where the
/// scope is resolved and never at query time. The org axis is a permission scope inside the
/// tenant wall, not a second wall — so the answer is a SET of organizations rather than one,
/// and a subtree taken at the root org is every branch under it exactly as one taken at a
/// branch is its own.</summary>
public sealed class OrgScope
{
    /// <summary>Every branch, because the work stands in none: a background job, a first
    /// touch, a migration, a design-time context, an anonymous request. It is the org axis'
    /// answer to what <c>Guid.Empty</c> is on the tenant axis — not "no scope resolved, so read
    /// nothing", but "this work is the company's own, not a branch's". A job that could read no
    /// branch's rows would sweep nothing and write rows nobody can see.</summary>
    public static readonly OrgScope Everywhere = new(null);

    /// <summary>No branch at all — the empty subtree, and the state that reads nothing.</summary>
    public static readonly OrgScope Nowhere = new([]);

    readonly Guid[]? _organizations;

    OrgScope(Guid[]? organizations)
    {
        _organizations = organizations;
    }

    /// <summary>The visible branches, or null where every branch is visible.</summary>
    public IReadOnlyCollection<Guid>? Organizations
    {
        get { return _organizations; }
    }

    public bool IsEverywhere
    {
        get { return _organizations is null; }
    }

    // An array rather than the interface, and the same one every query of the operation reads:
    // what the filter hands EF is a parameter, and a fresh collection per query would be a
    // fresh parameter per query.
    internal Guid[] Visible
    {
        get { return _organizations ?? []; }
    }

    public static OrgScope Of(IEnumerable<Guid> organizations)
    {
        ArgumentNullException.ThrowIfNull(organizations);

        return new OrgScope(organizations.Distinct().ToArray());
    }
}
