// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSail.Security;

/// <summary>A policy at rest: the document an admin reads, names and edits. Hydrated
/// into a PolicyHandler for evaluation. The Name is documentation, not semantics — a
/// stored row's is the words its owner chose, a built-in's is a localization key
/// (AddBuiltInPolicy), and neither is ever what identifies a policy.</summary>
public sealed class Policy
{
    /// <summary>The stored row's id, assigned by the store after a successful parse — not
    /// part of the document itself (a built-in or a freshly-built one has none), but the
    /// only way a load-time failure downstream of parsing (an unknown message, an unbound
    /// field) can be reported back against the row that caused it.</summary>
    [JsonIgnore]
    public Guid? Id { get; set; }

    public required string Name { get; init; }

    /// <summary>The use case's messages, as metadata keys or patterns:
    /// ["...CreatePrescription", "...ListPrescriptions"], ["Directory.Parties.*"], ["*"].
    /// Always a list, including for a wildcard — a single shape parses one way and the
    /// editor authors a checklist either way. A wildcard may carry Fields: it is expanded
    /// against the registry before a constraint is bound, and a message that joins the pattern
    /// later can only ever arrive narrowed (SecurityManager.Expand).</summary>
    public required IReadOnlyList<string> Messages { get; init; }

    /// <summary>Null = anonymous: matches without a session.</summary>
    public PolicyAudience? Audience { get; init; }

    /// <summary>Constraints on message fields, keyed by wire name. Null/empty = flat
    /// policy (full capability).</summary>
    public IReadOnlyDictionary<string, PolicyConstraint>? Fields { get; init; }

    /// <summary>The reads this grant allows are not narrowed to the organization the caller
    /// stands at: the org filter takes every branch unless the message itself names one
    /// (data.md, The org filter). What a person holds towards the company rather than towards
    /// one of its counters is read this way — a customer's own record is one record wherever
    /// it was written — and it is AUTHORED here, on the grant, never derived from the role or
    /// from where the session stands. False by default, so every row that does not say it
    /// keeps the caller's own branch.</summary>
    public bool EveryOrganization { get; init; }

    public DateOnly? ValidFrom { get; init; }

    public DateOnly? ValidTo { get; init; }
}

/// <summary>Who a policy applies to. All specified conditions AND together.</summary>
public sealed class PolicyAudience
{
    /// <summary>One person, by party.</summary>
    public Guid? Is { get; init; }

    /// <summary>A membership role code (roles only exist via Membership).</summary>
    public string? HasRole { get; init; }

    /// <summary>Membership in one organization, and — with Cascade — in the organizations
    /// under it. Different from HasRole, which asks nothing about where.</summary>
    public PolicyMembership? MemberOf { get; init; }

    /// <summary>Any authenticated session.</summary>
    public bool Authenticated { get; init; }
}

/// <summary>The membership half of an audience: who belongs where, and how far down the
/// organization chart the grant reaches.</summary>
public sealed class PolicyMembership
{
    /// <summary>Null = the symbol "@current": resolved at evaluation against the caller's
    /// own Session.OrganizationId, so one policy row covers the audience in whichever
    /// organization the actor is currently acting from instead of one row per organization.
    /// A literal id pins the audience to that one organization regardless of the session.</summary>
    [JsonConverter(typeof(PolicyOrganizationConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public Guid? Organization { get; init; }

    /// <summary>The membership's role code. Null = any role.</summary>
    public string? Role { get; init; }

    /// <summary>Members of the organizations under this one qualify too — the everyday
    /// case, because the org axis models one company's internal hierarchy and looking down
    /// it is what a grant usually means.</summary>
    public bool Cascade { get; init; }
}

public enum PolicyOrigin
{
    Stored = 1,

    BuiltIn = 2,

    Dependency = 3
}

/// <summary>The self-discriminating shape for PolicyMembership.Organization: the symbol
/// "@current" or a literal organization id, both a bare JSON string — the same pattern the
/// document already uses for its other constraint values, so a stored row reads as plainly
/// as the rest of the document instead of hiding the symbol behind an extra property.</summary>
sealed class PolicyOrganizationConverter : JsonConverter<Guid?>
{
    const string Current = "@current";

    // Without this, System.Text.Json short-circuits a null Nullable<Guid> straight to JSON
    // null before the converter ever runs — the symbol would never be written.
    public override bool HandleNull => true;

    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();

        return string.Equals(text, Current, StringComparison.Ordinal) ? null : Guid.Parse(text!);
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value is { } organization)
        {
            writer.WriteStringValue(organization);
        }
        else
        {
            writer.WriteStringValue(Current);
        }
    }
}
