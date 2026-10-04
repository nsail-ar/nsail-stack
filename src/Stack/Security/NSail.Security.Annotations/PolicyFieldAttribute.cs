// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security.Annotations;

public enum RestrictAs
{
    Party = 1,

    Organization = 2,

    /// <summary>A catalog reference — a role, a type, a code table row. Literal-only: the
    /// policy carries the ids the caller may pass and nothing else, because no symbol relates
    /// a catalog row to the actor the way @me relates a party.</summary>
    Reference = 3,

    /// <summary>A flag the caller either sets or does not — an override, an exception, a
    /// waiver. Literal-only for the same reason Reference is: a flag holds no relation to the
    /// actor, so the policy carries true or false and no symbol can answer for it. The one
    /// kind whose values are not ids.</summary>
    Flag = 4
}

/// <summary>Marks a message field as an entry of the policy metamodel (IPolicyField):
/// policies may restrict the values a caller passes in it. Unmarked fields are invisible
/// to security. The attribute declares the field's nature; the rule lives in the policy
/// (data, admin-editable).</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class PolicyFieldAttribute : Attribute
{
    public PolicyFieldAttribute(RestrictAs kind)
    {
        Kind = kind;
    }

    public RestrictAs Kind { get; }
}
