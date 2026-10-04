// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Not a bool: carries which policies allowed the send — "why do I have access"
/// and audit logging fall out of it for free.</summary>
public sealed class AuthorizationResult
{
    public required bool Allowed { get; init; }

    public IReadOnlyList<PolicyHandler> Matches { get; init; } = [];

    /// <summary>Whether what allowed the send read its fields. An implication
    /// (<see cref="DependencyPolicyHandler"/>) answers the key and the audience and ignores the
    /// values on purpose, so a send it ALONE allowed carries no value a policy looked at — which
    /// is why the org filter reads the caller's own branch for it and not the one the message
    /// names (data-tenancy.md, The org filter). Matching nothing is the system's own send: no policy was
    /// consulted, so no value was left unread.</summary>
    public bool FieldsVetted
    {
        get { return Matches.Count == 0 || Matches.Any(match => match.Origin != PolicyOrigin.Dependency); }
    }

    /// <summary>Whether what allowed the send was authored across every branch
    /// (<see cref="Policy.EveryOrganization"/>), which is what tells the org filter not to
    /// narrow to the one the caller stands at. One match saying so is enough: a caller holding
    /// both a counter's grant and their own reads the send each of them allows, and the send in
    /// hand is the one whose values the wider grant accepted.</summary>
    public bool EveryOrganization
    {
        get { return Matches.Any(match => match.Policy.EveryOrganization); }
    }

    public static AuthorizationResult Deny { get; } = new() { Allowed = false };
}
