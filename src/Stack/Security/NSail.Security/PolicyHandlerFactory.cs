// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Security.Annotations;

namespace NSail.Security;

/// <summary>Registered by the Policies.Handlers generator: how to hydrate a policy with
/// field constraints into its message's typed handler.</summary>
public sealed class PolicyHandlerFactory
{
    public PolicyHandlerFactory(Type messageType, Func<Policy, PolicyHandler> create)
    {
        MessageType = messageType;
        Create = create;
    }

    public Type MessageType { get; }

    public Func<Policy, PolicyHandler> Create { get; }

    /// <summary>The message's [PolicyField] names, each with the axis it is restricted as.
    /// A policy is multi-message, so a constraint is checked against the union of its
    /// members: this is what tells a constraint bound to some members apart from one bound
    /// to none, which is a typo. The axis travels with the name because an editor that
    /// offered "@me" on an organization field would author a grant the evaluator can only
    /// ever deny — a silent denial is the one failure a permission must not have.</summary>
    public IReadOnlyDictionary<string, RestrictAs> Fields { get; init; } = new Dictionary<string, RestrictAs>(StringComparer.Ordinal);

    /// <summary>The messages this one declares in [Requires] — the implication the evaluator
    /// turns into a dependency handler, so whoever may send this message may send them. It
    /// travels beside Fields, from the same generated registration, for the same reason: the
    /// declaration is the contract's, read once at boot, never per send.</summary>
    public IReadOnlyList<Type> Requires { get; init; } = [];
}
