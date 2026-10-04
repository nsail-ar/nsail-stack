// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>The .NET-free identity: who is acting and from where. Adapted once from
/// ClaimsPrincipal at the edge (Iam); handlers and services never see .NET security
/// types. The active OrganizationId is session state — switching it is a security event
/// (caches invalidate, effective policies refetch).</summary>
public class Session
{
    public bool IsAuthenticated { get; set; }

    public bool IsSystem { get; set; }

    public Guid? UserId { get; set; }

    public Guid? PartyId { get; set; }

    public string? Identity { get; set; }

    public string? DisplayName { get; set; }

    public string? ProviderId { get; set; }

    /// <summary>The actor's own record was founded by an external sign-up out of a provider's
    /// claims alone, so nobody has entered who they are yet. It is a session fact and not a
    /// database read because everything that consumes it — the router's gate above all —
    /// answers without going anywhere, and it stops being true the moment the person completes
    /// their sign-up, which re-issues the credential that carries it.</summary>
    public bool IsProvisional { get; set; }

    /// <summary>The role codes that hold where the session stands — <see cref="Standing"/>'s,
    /// derived at the edge whenever the organization is read, so an audience that asks for a
    /// role never meets one held somewhere else.</summary>
    public List<string> Roles { get; set; } = [];

    public Guid? OrganizationId { get; set; }

    /// <summary>The host the request arrived on, bare — no scheme, no port. Read once at the
    /// edge because it is a request-scoped fact every layer below would otherwise reach for
    /// HttpContext to learn, and it is what places an anonymous caller: the organization
    /// resolution reads it as the tenant's own base domain with, at most, one organization's
    /// label in front of it. Null off a request (background jobs, tests, the client's own
    /// rebuilt session).</summary>
    public string? Domain { get; set; }

    /// <summary>The tenant this identity was minted for, as its slug — the one thing in a
    /// session that the request cannot be asked for, because it is what the request is checked
    /// against: the credential says where it was issued and the install says where it arrived.
    /// Null off a tenant-resolving install, where there is no second place a credential could
    /// have come from.</summary>
    public string? Tenant { get; set; }

    /// <summary>Every membership the actor held when this Session was built, wherever it
    /// stands, each with the organizations above it already walked — what the selector offers;
    /// what grants is <see cref="Standing"/>. Materialized here because it is a graph query
    /// and everything downstream — audience matching on every send, every visibility
    /// question — has to answer without going to the database. The set is therefore as of
    /// session build (sign-in): a membership granted mid-session reaches the actor on the
    /// next one.</summary>
    public List<SessionMembership> Memberships { get; set; } = [];

    /// <summary>The memberships that hold where the session stands: the one at
    /// <see cref="OrganizationId"/> and those at an organization above it. A role holds at its
    /// membership's organization and below, so a membership anywhere else grants nothing here —
    /// the selector still offers it, and standing there is what makes it hold. Empty while the
    /// session stands nowhere.</summary>
    public IEnumerable<SessionMembership> Standing()
    {
        if (OrganizationId is not { } current)
        {
            return [];
        }

        var above = Memberships
            .Where(m => m.OrganizationId == current)
            .SelectMany(m => m.Ancestors)
            .ToHashSet();

        return Memberships.Where(m => m.OrganizationId == current || above.Contains(m.OrganizationId));
    }

    /// <summary>Background jobs act as the system: every send is allowed.</summary>
    public static Session System()
    {
        return new()
        {
            IsAuthenticated = true,
            IsSystem = true,
            DisplayName = "System"
        };
    }
}
