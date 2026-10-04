// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Authors a policy document in C#. It produces data and never a delegate: what
/// comes out is the same document an admin edits and the same one the evaluator hydrates,
/// so a seeded policy and a typed one cannot behave differently.
///
/// Messages are named by key rather than by type. A stored policy holds the key as a string
/// — that is what travels and what an editor shows — and the place this is used most, a
/// data migration, has no container to resolve a type through.</summary>
public sealed class PolicyBuilder
{
    readonly string _name;
    readonly List<string> _messages = [];
    readonly Dictionary<string, PolicyConstraint> _fields = [];
    PolicyAudience? _audience;
    DateOnly? _from;
    DateOnly? _to;
    bool _everyOrganization;

    PolicyBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _name = name;
    }

    /// <summary>The name is documentation the auditor reads, not semantics.</summary>
    public static PolicyBuilder Named(string name)
    {
        return new PolicyBuilder(name);
    }

    public PolicyBuilder For(params string[] messageKeys)
    {
        ArgumentNullException.ThrowIfNull(messageKeys);

        _messages.AddRange(messageKeys);

        return this;
    }

    /// <summary>No audience: the policy matches without a session. Legal, and one step from
    /// an unauthenticated API — say it out loud rather than by omission.</summary>
    public PolicyBuilder ForAnyone()
    {
        _audience = null;

        return this;
    }

    /// <summary>Any session, whatever its roles — the audience self-service needs, where
    /// naming a role or a party would be exactly wrong.</summary>
    public PolicyBuilder ForAnySession()
    {
        _audience = new PolicyAudience { Authenticated = true };

        return this;
    }

    public PolicyBuilder ForRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        _audience = new PolicyAudience { HasRole = role };

        return this;
    }

    /// <summary>Members of an organization — with cascade, of everything under it too.
    /// A null role takes any membership in it. A null organizationId is the symbol
    /// "@current": the audience follows whichever organization the caller is acting from,
    /// resolved at evaluation against the session rather than pinned at authoring time.</summary>
    public PolicyBuilder ForMembers(Guid? organizationId, string? role = null, bool cascade = false)
    {
        _audience = new PolicyAudience
        {
            MemberOf = new PolicyMembership
            {
                Organization = organizationId,
                Role = role,
                Cascade = cascade,
            }
        };

        return this;
    }

    public PolicyBuilder ForParty(Guid partyId)
    {
        _audience = new PolicyAudience { Is = partyId };

        return this;
    }

    public PolicyBuilder Between(DateOnly? from, DateOnly? to)
    {
        _from = from;
        _to = to;

        return this;
    }

    /// <summary>Constrains what the caller may pass in a field. The constraint applies to
    /// every listed message that carries the field and is silent on the ones that do not.</summary>
    public PolicyBuilder Restrict(string field, PolicyConstraint constraint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(constraint);

        _fields[field] = constraint;

        return this;
    }

    /// <summary>The reads this grant allows are the actor's in every branch, not the ones the
    /// branch they stand at holds (<see cref="Policy.EveryOrganization"/>). Said on the grant
    /// because only the grant knows whether what it hands over belongs to the company or to one
    /// of its counters.</summary>
    public PolicyBuilder AcrossEveryOrganization()
    {
        _everyOrganization = true;

        return this;
    }

    public Policy Build()
    {
        if (_messages.Count == 0)
        {
            throw new InvalidOperationException($"Policy '{_name}': a policy must list at least one message.");
        }

        return new Policy
        {
            Name = _name,
            Messages = _messages.Distinct(StringComparer.Ordinal).ToList(),
            Audience = _audience,
            Fields = _fields.Count > 0 ? _fields : null,
            EveryOrganization = _everyOrganization,
            ValidFrom = _from,
            ValidTo = _to,
        };
    }
}
