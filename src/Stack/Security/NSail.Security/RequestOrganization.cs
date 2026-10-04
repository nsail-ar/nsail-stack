// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Which organization an anonymous request belongs to — and, when the install
/// cannot tell, that it cannot. The third state is not a null: a request nobody claims
/// while several organizations could answer for it must never be given one, so the
/// ambiguity travels instead of being flattened into "none".</summary>
public sealed class RequestOrganization
{
    public static RequestOrganization None { get; } = new();

    public static RequestOrganization Ambiguous { get; } = new() { IsAmbiguous = true };

    public Guid? Id { get; private init; }

    public bool IsAmbiguous { get; private init; }

    public static RequestOrganization For(Guid id)
    {
        return new RequestOrganization { Id = id };
    }
}
