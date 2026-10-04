// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Answers the Directory-graph questions the constraint vocabulary needs. The
/// base is deliberately optimistic (always true): it serves the WASM client (no graph
/// there — the server gate is the real check) and products without Iam. Iam registers
/// the DB-backed implementation server-side.</summary>
public class RelationProvider
{
    public virtual Task<bool> IsRelated(Guid partyId, Guid relatedPartyId, string role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public virtual Task<bool> IsMember(Guid partyId, Guid organizationId, string? role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public virtual Task<bool> IsMemberOrDescendant(Guid partyId, Guid organizationId, string? role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <summary>The organizations above this one, nearest first. The one expansion of the
    /// organization chart there is: the descendant questions above are this walk read from
    /// the other end, and a session materializes its memberships through it.</summary>
    public virtual Task<IReadOnlyList<Guid>> GetAncestors(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    /// <summary>The organization and everything under it — the branches an actor standing at it
    /// can see, which is what the org filter is set to (data-tenancy.md). The same chart the walk above
    /// climbs, read downward, and the reason it is a walk rather than a question asked inside a
    /// query: a WHERE cannot follow a parent link, and the answer is one set per operation
    /// rather than one per row. The base answers the organization alone — the client holds no
    /// chart and asks the server for every row it draws anyway.</summary>
    public virtual Task<IReadOnlyList<Guid>> GetDescendants(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Guid>>([organizationId]);
    }
}
