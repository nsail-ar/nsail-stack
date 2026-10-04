// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>The behavior half of a [Requires] declaration: whoever may send the primary
/// message may send the one it declares needing. It holds no document an admin can edit —
/// the implication is the contract's, fixed at deploy — so the policy it carries exists only
/// to name itself in an authorization result and in the editor's grey implication row.
/// <para>One hop by construction: the question it asks is answered by the stored and
/// built-in handlers alone, so a dependency can never grant a second dependency.</para>
/// <para>The implication is flat: with the key and the audience answered, there is nothing
/// left to constrain — an explicit narrowed policy on the required message composes as
/// another OR branch and cannot restrict below this one.</para>
/// <para>Ignoring the fields is what keeps the row filter off them too: a send this handler
/// ALONE allowed names no branch the gate approved, so the org filter reads the caller's own
/// (<see cref="AuthorizationResult.FieldsVetted"/>, data-tenancy.md, The org filter). Without that, the
/// whole dropdown this exists to serve would be every branch's dropdown, and no policy could
/// narrow it.</para></summary>
public sealed class DependencyPolicyHandler : PolicyHandler
{
    readonly Func<string, Session, DateOnly, bool> _granted;

    public DependencyPolicyHandler(string messageKey, string primaryKey, Func<string, Session, DateOnly, bool> granted)
        : base(new Policy { Name = $"Implied by {primaryKey}", Messages = [messageKey] })
    {
        ArgumentNullException.ThrowIfNull(messageKey);
        ArgumentNullException.ThrowIfNull(primaryKey);
        ArgumentNullException.ThrowIfNull(granted);

        _granted = granted;
        PrimaryKey = primaryKey;
        MessageKey = messageKey;
        Origin = PolicyOrigin.Dependency;
    }

    /// <summary>The message that declared the implication — the "why do I have access"
    /// answer for a send this handler allowed.</summary>
    public string PrimaryKey { get; }

    /// <summary>Satisfiability of the primary, which is the whole audience question here:
    /// the fields of the primary's own policy are ignored on purpose (a caller narrowed to
    /// their own patients still needs the whole dropdown to pick one).</summary>
    public override bool AppliesTo(Session session, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(session);

        return _granted(PrimaryKey, session, today);
    }

    public override Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}
