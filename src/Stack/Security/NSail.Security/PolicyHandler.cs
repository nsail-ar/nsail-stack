// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Security.Annotations;

namespace NSail.Security;

/// <summary>A policy hydrated for evaluation — the behavior half of the Policy/PolicyHandler
/// pair (data travels, behavior evaluates). Owns ALL evaluation semantics: key matching,
/// audience, validity and the constraint vocabulary; generated handlers only dispatch to
/// Satisfies with their typed field accessors.</summary>
public abstract class PolicyHandler
{
    protected PolicyHandler(Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        Policy = policy;
    }

    public Policy Policy { get; }

    /// <summary>Assigned by the source that hydrated the handler, never by the author —
    /// it drives the editor's displays and the "why do I have access" answer.</summary>
    public PolicyOrigin Origin { get; set; } = PolicyOrigin.BuiltIn;

    /// <summary>The one message this handler was hydrated for, assigned at hydration like
    /// Origin. A policy lists several and expands into one handler each, so patterns are
    /// resolved at load and nothing matches a pattern at evaluation time.</summary>
    public string MessageKey { get; set; } = string.Empty;

    /// <summary>Key + audience + validity — everything except the fields.
    /// Satisfiability: "could this policy ever allow this caller?"</summary>
    public bool AppliesTo(string messageKey, Session session, DateOnly today)
    {
        return MessageKey == messageKey && AppliesTo(session, today);
    }

    /// <summary>The caller half alone, with no message in hand: what the effective-policies
    /// response filters on, so the client receives the rows that could ever apply to it and
    /// never has to evaluate an audience itself.</summary>
    public virtual bool AppliesTo(Session session, DateOnly today)
    {
        if (Policy.ValidFrom is { } from && today < from)
            return false;

        if (Policy.ValidTo is { } to && today > to)
            return false;

        var audience = Policy.Audience;

        if (audience is null)
            return true;

        if (!session.IsAuthenticated)
            return false;

        if (audience.Is is { } party && session.PartyId != party)
            return false;

        if (audience.HasRole is { } role && !session.Roles.Contains(role))
            return false;

        if (audience.MemberOf is { } membership && !IsMember(session, membership))
            return false;

        return true;
    }

    /// <summary>Membership, answered from the Session alone. This whole method chain is
    /// synchronous on purpose — it runs on every send and on every menu, page and action the
    /// UI draws — so the graph question it would otherwise ask the database is answered from
    /// what the Session already carries, walked once when the Session was built.</summary>
    static bool IsMember(Session session, PolicyMembership membership)
    {
        // Null is the symbol "@current" — resolved here, against the caller's own active
        // organization, so the constraint vocabulary never leaks into the membership walk
        // below: by the time it runs, there is only ever a target id or nothing to match.
        if ((membership.Organization ?? session.OrganizationId) is not { } organization)
            return false;

        // Only what holds where the session stands: a membership elsewhere — below it, beside
        // it — grants nothing here, and one above it holds here as if it were here.
        foreach (var held in session.Standing())
        {
            if (membership.Role is { } role && held.Role != role)
                continue;

            if (held.OrganizationId == organization || organization == session.OrganizationId)
                return true;

            if (membership.Cascade && held.Ancestors.Contains(organization))
                return true;
        }

        return false;
    }

    /// <summary>The fields verdict, with the message instance in hand.</summary>
    public abstract Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken);

    protected async Task<bool> Satisfies(string field, Guid? value, RestrictAs kind, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        if (ConstraintOn(field) is not { } constraint)
        {
            return true;
        }

        if (value is not { } id)
        {
            return Omitted(constraint, kind);
        }

        return await Satisfies(constraint, id, kind, session, relations, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>What a constrained field left empty means. Everywhere but one case: nothing —
    /// the caller must pass the qualifying value, because no second door narrows the read for
    /// them and an omission would hand over the unfiltered view the constraint exists to close.
    /// <para>The one case is the ORGANIZATION axis constrained to @memberOfOrDescendant. There a
    /// second door does narrow it: a message that names no branch is scoped by the row filter to
    /// the seat's own subtree (data.md, The org filter — "every by-id read and every
    /// transition"), and that subtree is inside what this symbol already permits, so refusing the
    /// omission would refuse the only send those hundreds of by-id reads make while widening
    /// nothing.</para>
    /// <para>The other two org symbols are NOT that case, and the difference is the subtree.
    /// @current permits the seat alone and @memberOf permits the seat's memberships without
    /// descending; the filter's fallback is the seat PLUS everything below it, so an omission
    /// there would hand back branches that naming them would have refused — widening by
    /// omission, which is the thing this evaluator does not do. A LITERAL set rejects the
    /// omission for the same reason from the other end: the author named branches, and the seat
    /// is not one of them.</para></summary>
    static bool Omitted(PolicyConstraint constraint, RestrictAs kind)
    {
        return kind == RestrictAs.Organization
            && constraint.Kind is PolicyConstraintKind.MemberOfOrDescendant;
    }

    /// <summary>The collection shape of the same question, quantified universally: every
    /// element must satisfy the constraint, so one element outside it denies the whole
    /// message. The kind is the elements' — a list of party ids is restricted as Party, and
    /// "@me over the collection" means every attendee is the actor.</summary>
    protected async Task<bool> Satisfies(string field, IEnumerable<Guid>? values, RestrictAs kind, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        if (ConstraintOn(field) is not { } constraint)
        {
            return true;
        }

        if (values is null)
        {
            return false;
        }

        var quantified = false;

        foreach (var value in values)
        {
            quantified = true;

            if (!await Satisfies(constraint, value, kind, session, relations, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        // An empty collection is this shape's null. Universal quantification would pass it
        // vacuously, and the evaluator fails closed instead: a constrained field must not
        // widen by omission, and the caller who may only list themselves has to list
        // themselves rather than send nobody.
        return quantified;
    }

    /// <summary>The flag shape of the same question, and the only one that answers without
    /// the session: a flag holds no relation to the actor, so the policy pins it to true or
    /// false and the send has to carry that value. A constraint that arrived carrying ids
    /// instead — refused at both doors, so only by a road nobody authored — pins the field to
    /// nothing and denies, which is where this fails closed.</summary>
    protected bool Satisfies(string field, bool value)
    {
        if (ConstraintOn(field) is not { } constraint)
        {
            return true;
        }

        return constraint.Flag == value;
    }

    /// <summary>The constraint in force on a field, or null when there is none — absent from
    /// the document (lenient fields) and "*" are the same answer, and both mean free.</summary>
    PolicyConstraint? ConstraintOn(string field)
    {
        if (Policy.Fields is null || !Policy.Fields.TryGetValue(field, out var constraint))
        {
            return null;
        }

        return constraint.Kind == PolicyConstraintKind.Any ? null : constraint;
    }

    /// <summary>One value against one constraint. Reference has no case of its own: every
    /// symbol below is answered by an axis it is not, so the literal set is the only shape
    /// that can ever satisfy it — which is what "literal-only" means in the evaluator.</summary>
    async Task<bool> Satisfies(PolicyConstraint constraint, Guid id, RestrictAs kind, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return constraint.Kind switch
        {
            PolicyConstraintKind.Literal => constraint.Values.Contains(id),

            PolicyConstraintKind.Me =>
                kind == RestrictAs.Party && session.PartyId == id,

            PolicyConstraintKind.Current =>
                kind == RestrictAs.Organization && session.OrganizationId == id,

            PolicyConstraintKind.RelatedAs =>
                kind == RestrictAs.Party
                    && session.PartyId is { } party
                    && await relations.IsRelated(party, id, constraint.Role!, cancellationToken).ConfigureAwait(false),

            PolicyConstraintKind.MemberOf =>
                kind == RestrictAs.Organization
                    && session.PartyId is { } member
                    && await relations.IsMember(member, id, null, cancellationToken).ConfigureAwait(false),

            PolicyConstraintKind.MemberOfOrDescendant =>
                kind == RestrictAs.Organization
                    && session.PartyId is { } ancestor
                    && await relations.IsMemberOrDescendant(ancestor, id, null, cancellationToken).ConfigureAwait(false),

            _ => false
        };
    }

    /// <summary>"*", "Area.Feature.*" prefixes, or the exact key. Used at load to expand a
    /// policy's patterns against the registry, never per send.</summary>
    public static bool MatchesKey(string pattern, string key)
    {
        if (pattern == "*")
            return true;

        return pattern.EndsWith(".*", StringComparison.Ordinal)
            ? key.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : pattern == key;
    }
}
