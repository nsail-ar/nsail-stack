// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>One membership the actor holds: the organization, the role it was granted with,
/// and the chain of organizations above it. A cascading audience names an organization and
/// means it together with everything under it, so asking whether a membership answers to it
/// is asking whether the named organization stands on this membership's way up — which is
/// why the way up is walked once, where the Session is built, and never at evaluation.</summary>
public sealed class SessionMembership
{
    public Guid OrganizationId { get; set; }

    /// <summary>The role's code. Never absent: a membership always has a role, and a claim
    /// that names none is read as no membership at all.</summary>
    public required string Role { get; set; }

    /// <summary>The organizations above <see cref="OrganizationId"/>, nearest first. Empty
    /// when the membership is at a root organization.</summary>
    public List<Guid> Ancestors { get; set; } = [];
}
