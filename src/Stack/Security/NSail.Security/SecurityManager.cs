// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Dates;
using NSail.Messaging.Runtime;
using NSail.Problems;
using NSail.Security.Annotations;

namespace NSail.Security;

/// <summary>Aggregates policy sources (built-ins from DI; stored ones through LoadPolicies),
/// hydrates them into handlers, and answers the only question: may this session send this
/// message? One class for both worlds — on the server Iam's DbSecurityManager feeds it the
/// tenant's stored policies (PolicyStore) beside the built-ins, on the client the
/// authentication state provider feeds it the effective set; only the source differs.</summary>
public class SecurityManager
{
    readonly MessageRegistry _messages;
    readonly RelationProvider _relations;
    readonly Dictionary<Type, PolicyHandlerFactory> _factories;
    readonly List<PolicyHandler> _builtIn = [];
    readonly List<PolicyHandler> _stored = [];
    readonly List<DependencyPolicyHandler> _dependencies = [];
    readonly ILogger<SecurityManager> _logger;

    public SecurityManager(
        MessageRegistry messages,
        RelationProvider relations,
        IEnumerable<PolicyHandlerFactory> factories,
        IEnumerable<Policy> builtInPolicies,
        ILogger<SecurityManager>? logger = null)
    {
        _messages = messages;
        _relations = relations;
        _factories = factories.ToDictionary(f => f.MessageType);
        _logger = logger ?? NullLogger<SecurityManager>.Instance;

        // Built-ins are code: a built-in that fails to hydrate is a deploy-time bug, not a
        // stale row, so it stays a hard failure — the loop below exists for stored rows only.
        foreach (var policy in builtInPolicies)
        {
            _builtIn.AddRange(Hydrate(policy, PolicyOrigin.BuiltIn));
        }

        Imply();
    }

    /// <summary>The [Requires] declarations, turned into handlers once at boot. Nobody names
    /// the implied message anywhere else: the primary's contract declares it, the generated
    /// registration carries it, and this is where it starts granting.</summary>
    void Imply()
    {
        foreach (var factory in _factories.Values)
        {
            var primary = KeyFor(factory.MessageType);

            foreach (var required in factory.Requires)
            {
                _dependencies.Add(new DependencyPolicyHandler(KeyFor(required), primary, Grants));
            }
        }
    }

    /// <summary>What a dependency handler asks, and the one hop it gets: the stored and
    /// built-in sets only, so an implied grant can never imply a third message.</summary>
    bool Grants(string messageKey, Session session, DateOnly today)
    {
        return _builtIn.Concat(_stored).Any(handler => handler.AppliesTo(messageKey, session, today));
    }

    /// <summary>Every source, in the order an audit reads them: the code's, the admin's, and
    /// the ones the contract implies.</summary>
    IEnumerable<PolicyHandler> Handlers()
    {
        return _builtIn.Concat(_stored).Concat(_dependencies);
    }

    /// <summary>The code-owned policies, for an editor that has to show them read-only.
    /// Listed from the hydrated set rather than from the injected one: a built-in that did
    /// not hydrate is not in force, and showing it anyway would document a grant the
    /// evaluator does not have.</summary>
    public IReadOnlyList<Policy> BuiltInPolicies => _builtIn.Select(handler => handler.Policy).Distinct().ToList();

    /// <summary>Every message key the registry knows, which is what a policy editor builds
    /// its tree from — so the checklist can only offer keys a handler exists for. That is
    /// true forward in time only: a row saved against this list outlives it, and a message
    /// deleted afterwards leaves a key the editor has no node for and hydration drops.</summary>
    public IReadOnlyList<string> MessageKeys => _messages.Keys;

    /// <summary>The fields a policy over these messages may constrain, each with the axis it
    /// is restricted as — the union across the members, which is exactly what the editor's
    /// constraint panel authors against and what hydration will bind. A pattern contributes the
    /// fields of the messages it covers today, the same expansion hydration performs: a panel
    /// that offered nothing for a feature grant would hide a constraint the document carries,
    /// and the next save through the editor would drop it.</summary>
    public IReadOnlyDictionary<string, RestrictAs> GetConstrainableFields(IEnumerable<string> messageKeys)
    {
        ArgumentNullException.ThrowIfNull(messageKeys);

        var fields = new Dictionary<string, RestrictAs>(StringComparer.Ordinal);

        foreach (var key in Members(messageKeys))
        {
            if (_messages.Resolve(key) is not { } type)
            {
                continue;
            }

            if (!_factories.TryGetValue(type, out var factory))
            {
                throw new InvalidOperationException($"no policy handler is registered for '{key}'");
            }

            foreach (var (name, kind) in factory.Fields)
            {
                fields[name] = kind;
            }
        }

        return fields;
    }

    IEnumerable<string> Members(IEnumerable<string> messageKeys)
    {
        foreach (var key in messageKeys)
        {
            if (!key.Contains('*'))
            {
                yield return key;
                continue;
            }

            foreach (var covered in _messages.Keys.Where(candidate => PolicyHandler.MatchesKey(key, candidate)))
            {
                yield return covered;
            }
        }
    }

    /// <summary>The organizations this message names: the values standing in its
    /// [PolicyField(RestrictAs.Organization)] fields, read through the same registration the
    /// constraints bind through. A reading of the message and nothing more — it says what was
    /// asked for, never that asking was allowed, so a caller behind the gate pairs it with
    /// <see cref="AuthorizationResult.FieldsVetted"/> before acting on it (data-tenancy.md, The org
    /// filter). A message that declares no organization, or leaves it empty, names none.</summary>
    public IReadOnlyCollection<Guid> OrganizationsNamedBy(object message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_factories.TryGetValue(message.GetType(), out var factory))
        {
            return [];
        }

        return PolicyFields.OrganizationsOf(factory, message);
    }

    /// <summary>Replaces the stored set (built-ins are code and never move). Two failures,
    /// two sizes. A message key the registry cannot resolve costs its own grant and nothing
    /// else — it is dropped and the rest of the document hydrates, because a key no handler
    /// answers for granted nothing to begin with, so dropping it loosens nothing and the
    /// blast radius is the grant instead of the whole role. Every other failure (a malformed
    /// document, an off-axis constraint) still quarantines the row: deny-by-default makes
    /// that safe, and one bad row must not take the others — or every request — down with
    /// it. A drop also excuses the row's unbound constraints, which is the one thing that
    /// goes quiet, so it is reported beside the drop rather than swallowed.</summary>
    public virtual void LoadPolicies(IEnumerable<Policy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        _stored.Clear();

        foreach (var policy in policies)
        {
            var drift = new Drift();

            try
            {
                _stored.AddRange(Hydrate(policy, PolicyOrigin.Stored, drift));
            }
            catch (Exception exception)
            {
                OnQuarantined(policy, exception);
                continue;
            }

            if (drift.Messages.Count > 0)
            {
                OnDropped(policy, drift.Messages, drift.Excused);
            }
        }
    }

    /// <summary>Called once per stored policy that failed to hydrate. The log lives here
    /// rather than at the call site so an override can decide whether this load has already
    /// reported the row: the stored set re-hydrates per request, and the same stack printed
    /// on every one of them is how a single stale row becomes a wall of noise.
    /// DbSecurityManager also tells the store which row it was, so the admin UI can show it
    /// as broken.</summary>
    protected virtual void OnQuarantined(Policy policy, Exception exception)
    {
        _logger.LogError(exception, "Policy '{Name}' (id {Id}) failed to hydrate and was quarantined: it will grant nothing until fixed.", policy.Name, policy.Id);
    }

    /// <summary>Called once per stored policy that hydrated with some of its message keys
    /// unresolved: the row is in force, minus those grants. Same reason the log sits behind a
    /// virtual as above — this one repeats on every request until somebody edits the row.
    /// <para><paramref name="excused"/> is the amnesty the drop granted: the constrained
    /// fields no surviving member declares, which the drop lets through because the message
    /// that declared them may be the one that is gone (see Validate). They bind nothing
    /// either way, so the grant is not what is at stake — the silence is. A plain authoring
    /// typo rides out on the first dead key its row happens to carry, and this line is all
    /// that stands between it and nobody ever hearing about it.</para></summary>
    protected virtual void OnDropped(Policy policy, IReadOnlyList<string> messages, IReadOnlyList<string> excused)
    {
        _logger.LogWarning(
            "Policy '{Name}' (id {Id}) names {Count} message(s) the registry no longer knows and dropped them; the rest of the policy is in force. Dropped: {Messages}.",
            policy.Name,
            policy.Id,
            messages.Count,
            string.Join(", ", messages));

        if (excused is not { Count: > 0 })
        {
            return;
        }

        _logger.LogWarning(
            "Policy '{Name}' (id {Id}) constrains {Count} field(s) no message it still lists declares, excused by the drop above and binding nothing: {Fields}. One of the dropped messages may have declared them — or they are a typo that was a hydration error until the drop, so check them.",
            policy.Name,
            policy.Id,
            excused.Count,
            string.Join(", ", excused));
    }

    public virtual async Task<AuthorizationResult> Authorize(object message, Session session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(session);

        if (session.IsSystem)
            return new AuthorizationResult { Allowed = true };

        var key = KeyFor(message.GetType());
        var today = BusinessDate.Today;
        var matches = new List<PolicyHandler>();

        foreach (var handler in Handlers())
        {
            if (!handler.AppliesTo(key, session, today))
                continue;

            if (await handler.Authorize(message, session, _relations, cancellationToken).ConfigureAwait(false))
                matches.Add(handler);
        }

        return matches.Count > 0
            ? new AuthorizationResult { Allowed = true, Matches = matches }
            : AuthorizationResult.Deny;
    }

    /// <summary>Lets a caller that can await guarantee the policy source PreAuthorize is
    /// about to read synchronously is not still cold — PreAuthorize itself stays
    /// synchronous because most of its callers (menu, dashboard and action visibility
    /// checks in Blazor render code) cannot. A source with nothing to warm is already
    /// current, hence the no-op base: only DbSecurityManager's per-tenant store has a
    /// first load to wait for.</summary>
    public virtual Task EnsureLoaded(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>The optimistic gate for UI (menu, page and action visibility): may this
    /// session <em>ever</em> send this message type? It checks only whether some policy
    /// applies to the key for this session — field constraints, which need concrete values
    /// the UI doesn't have yet, are ignored (assumed satisfiable). Showing something the
    /// strict Authorize later denies is recoverable; hiding something allowed is not, so
    /// the UI errs on the permissive side and the Mediator stays the real guard.</summary>
    public virtual bool PreAuthorize(Type messageType, Session session)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(session);

        if (session.IsSystem)
            return true;

        var key = KeyFor(messageType);
        var today = BusinessDate.Today;

        return Handlers().Any(handler => handler.AppliesTo(key, session, today));
    }

    /// <summary>PreAuthorize for a (possibly partial) message instance — same optimistic
    /// rule, keyed off the runtime type (object, like Authorize, so Security stays free of
    /// the messaging dependency). Present fields don't tighten the gate today; they could
    /// later refine it without changing callers.</summary>
    public bool PreAuthorize(object message, Session session)
    {
        ArgumentNullException.ThrowIfNull(message);

        return PreAuthorize(message.GetType(), session);
    }

    /// <summary>Whether this field's only satisfiable value, for this session and message, is
    /// the caller's own party — every policy that could ever allow the send constrains the
    /// field to @me, and none leaves it free (or absent, which is free by the lenient-fields
    /// rule). The one client-derivable field state (permissions.md) that resolves without a
    /// graph query: @me compares against the Session the client already holds. A list page
    /// reads this to default its party filter to Session.PartyId instead of sending
    /// unfiltered and losing to a constrained field's null rejection.</summary>
    public bool RequiresMe(Type messageType, string field, Session session)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(session);

        if (session.IsSystem || session.PartyId is null)
            return false;

        var key = KeyFor(messageType);
        var today = BusinessDate.Today;
        var matched = false;

        foreach (var handler in Handlers())
        {
            if (!handler.AppliesTo(key, session, today))
                continue;

            matched = true;

            if (handler.Policy.Fields?.GetValueOrDefault(field)?.Kind != PolicyConstraintKind.Me)
                return false;
        }

        return matched;
    }

    /// <summary>Whether some policy that could allow this send leaves this flag field free or
    /// pins it to this value — the Flag axis's client-derived field state (permissions.md).
    /// A flag is a literal the browser can decide on its own, with no graph and no lookup, so
    /// a checkbox the caller may not tick is not drawn instead of being drawn and refused.
    /// Optimistic like every other client-side check: it reads no other field, so it answers
    /// no only when nothing could ever satisfy the value, and the gate still decides.</summary>
    public bool Permits(Type messageType, string field, bool value, Session session)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(session);

        if (session.IsSystem)
        {
            return true;
        }

        var key = KeyFor(messageType);
        var today = BusinessDate.Today;

        foreach (var handler in Handlers())
        {
            if (!handler.AppliesTo(key, session, today))
            {
                continue;
            }

            var constraint = handler.Policy.Fields?.GetValueOrDefault(field);

            if (constraint is null || constraint.Kind == PolicyConstraintKind.Any || constraint.Flag == value)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The policies that could ever apply to this session — audience and validity
    /// matched, message key untouched. It is what the client is given: filtering by audience
    /// here is what lets the client's check collapse to a key lookup, so no evaluator logic
    /// is duplicated in WASM and the two sides cannot drift.</summary>
    public virtual IReadOnlyList<Policy> GetEffectivePolicies(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var today = BusinessDate.Today;

        // Distinct by policy: expansion produced a handler per member message, and what the
        // client is given is the document, once.
        return Handlers()
            .Where(handler => handler.AppliesTo(session, today))
            .Select(handler => handler.Policy)
            .Distinct()
            .ToList();
    }

    string KeyFor(Type messageType)
    {
        return _messages.KeyFor(messageType);
    }

    PolicyHandlerFactory FactoryFor(string key)
    {
        var type = _messages.Resolve(key)!;

        if (!_factories.TryGetValue(type, out var factory))
        {
            throw new InvalidOperationException($"no policy handler is registered for '{key}'");
        }

        return factory;
    }

    /// <summary>Answers the editor before a document is stored, so a policy that could never
    /// work is refused where someone can still fix it. Hydration asks the same questions
    /// again and throws: this is the courteous door, not the only one.</summary>
    public virtual PolicyValidation ValidateDocument(Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var errors = new List<Issue>();

        if (policy.Messages is null or { Count: 0 })
        {
            errors.Add(new Issue("PolicyWithoutMessages", "A policy must list at least one message."));
        }

        var keys = new List<string>();

        foreach (var pattern in policy.Messages ?? [])
        {
            if (pattern.Contains('*'))
            {
                keys.AddRange(_messages.Keys.Where(key => PolicyHandler.MatchesKey(pattern, key)));
                continue;
            }

            if (_messages.Resolve(pattern) is not null)
            {
                keys.Add(pattern);
                continue;
            }

            errors.Add(new Issue("UnknownMessage", $"No message is registered for '{pattern}'.", pattern));
        }

        foreach (var (field, constraint) in policy.Fields ?? new Dictionary<string, PolicyConstraint>())
        {
            // Bound to some members and not others is deliberate and silent; bound to none
            // is a typo or a field nobody marked [PolicyField], and both are dead.
            if (!keys.Any(key => FactoryFor(key).Fields.ContainsKey(field)))
            {
                errors.Add(new Issue("UnboundConstraint", $"No message this policy lists declares '{field}' as a [PolicyField].", field));
                continue;
            }

            // Asked first, and alone: a constraint the field's axis cannot answer at all is
            // one fault, and "restricted to a list of values and none was chosen" said of a
            // flag beside it would send the author looking for a second thing to fix.
            if (OffAxis(field, constraint, keys) is { } offAxis)
            {
                errors.Add(offAxis);
                continue;
            }

            // The same refusals the JSON converter makes at load, asked here where the answer
            // is a message in front of the author: a constraint the evaluator can never
            // satisfy is a grant that silently denies, and stored it would only surface as a
            // quarantined row nobody edited.
            if (constraint.Kind == PolicyConstraintKind.Literal && constraint.Values.Count == 0 && constraint.Flag is null)
            {
                errors.Add(new Issue("ConstraintWithoutValues", $"'{field}' is restricted to a list of values and none was chosen, which denies every send instead of granting one.", field));
            }

            if (constraint.Kind == PolicyConstraintKind.RelatedAs && string.IsNullOrWhiteSpace(constraint.Role))
            {
                errors.Add(new Issue("ConstraintWithoutRole", $"'{field}' is restricted to a relationship and no role was chosen.", field));
            }
        }

        return new PolicyValidation(errors, policy.Audience is null);
    }

    /// <summary>One hydrated handler per member message: a policy is a use case, so its
    /// keys are resolved here — wildcards against the registry, concrete keys checked to
    /// exist — and evaluation never sees a pattern again.
    /// <para>The collector is the permission to drop an unresolvable key, and a built-in
    /// hydrates without one: its keys are code, so one the registry does not know is a
    /// deploy-time bug that must stay loud (see the constructor).</para></summary>
    List<PolicyHandler> Hydrate(Policy policy, PolicyOrigin origin, Drift? drift = null)
    {
        var keys = Expand(policy, drift);

        Validate(policy, keys, drift);

        var handlers = new List<PolicyHandler>(keys.Count);

        foreach (var key in keys)
        {
            var handler = FactoryFor(key).Create(policy);

            handler.Origin = origin;
            handler.MessageKey = key;

            handlers.Add(handler);
        }

        return handlers;
    }

    /// <summary>A policy's message keys, patterns expanded against the registry.
    /// <para>A pattern rides beside field constraints: expansion happens here, BEFORE anything is
    /// validated, so the constraint is bound and checked against the keys the pattern actually
    /// covers. What a pattern cannot promise is the message that joins it tomorrow — and that
    /// direction is safe, because a constraint only ever narrows: a send the new message would
    /// have carried freely arrives constrained, never the other way round. That is what lets a
    /// feature grant arm the org axis (`Products.*` with `OrganizationId: @memberOfOrDescendant`)
    /// instead of an enumerated list that is one message out of date by the next release.</para></summary>
    IReadOnlyList<string> Expand(Policy policy, Drift? drift)
    {
        if (policy.Messages is null or { Count: 0 })
            throw new InvalidOperationException($"Policy '{policy.Name}': a policy must list at least one message.");

        // Distinct: overlapping patterns ("*" plus a key it already covers) would otherwise
        // hydrate the same message twice and report the policy twice in the match list.
        var keys = new List<string>();

        foreach (var pattern in policy.Messages)
        {
            if (pattern.Contains('*'))
            {
                keys.AddRange(_messages.Keys.Where(key => PolicyHandler.MatchesKey(pattern, key)));
                continue;
            }

            if (_messages.Resolve(pattern) is null)
            {
                // A stored row was written against the registry of the day it was saved, and
                // a message deleted since is not the row's fault: the grant it can no longer
                // name goes (it granted nothing already) and the rest of the row stands.
                if (drift is not null)
                {
                    drift.Messages.Add(pattern);
                    continue;
                }

                throw new InvalidOperationException($"Policy '{policy.Name}': no policy handler is registered for '{pattern}' — is the message an IMessage in an Sdk with a generated [Generated(Policies.Handlers)] holder?");
            }

            keys.Add(pattern);
        }

        return keys.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>A constrained field that no member declares is a typo or a dead constraint,
    /// so it fails loudly. One that only <em>some</em> members declare is deliberate and
    /// silent: UpdatePrescription drops PatientId because the subject is immutable, and
    /// forbidding that would forbid the verb family the feature exists for.</summary>
    void Validate(Policy policy, IReadOnlyList<string> keys, Drift? drift)
    {
        if (policy.Fields is null or { Count: 0 })
            return;

        foreach (var (field, constraint) in policy.Fields)
        {
            if (!keys.Any(key => FactoryFor(key).Fields.ContainsKey(field)))
            {
                // The excuse is the row's, not this field's. A member was dropped above, so
                // the only message that declared this field may be the one that is gone —
                // and it took its [PolicyField] declarations with it, so "was THIS field the
                // dropped key's" has no answer left to ask. The answerable question, "does
                // some LIVE message declare it", would quarantine exactly the row the drop
                // exists to save: the one whose dead message owned the field name alone. So
                // every unbound field in a row that dropped something rides out, a plain
                // typo included. Confinement is not what that costs — under lenient fields a
                // constraint no surviving member declares is a no-op and can never loosen
                // what survives (permissions.md) — the signal is, so the collector carries
                // the field to a report rather than swallowing it.
                //
                // With nothing dropped the field names no message that ever declared it, and
                // that is still the typo it always was.
                if (drift is { Messages.Count: > 0 })
                {
                    drift.Excused.Add(field);
                    continue;
                }

                throw new InvalidOperationException($"Policy '{policy.Name}': field '{field}' is constrained but no message it lists declares it as a [PolicyField].");
            }

            if (OffAxis(field, constraint, keys) is { } offAxis)
                throw new InvalidOperationException($"Policy '{policy.Name}': {offAxis.Message} The constraint could only ever deny.");
        }
    }

    /// <summary>What hydrating a stored row was allowed to let pass, collected for the
    /// report: the message keys the registry no longer resolves, and the constrained fields
    /// their departure left declared by nobody.</summary>
    sealed class Drift
    {
        public List<string> Messages { get; } = [];

        public List<string> Excused { get; } = [];
    }

    /// <summary>Reference and Flag are literal-only, and this is where those words are
    /// enforced rather than merely true. The evaluator already denies what the axis cannot
    /// answer, but a denial nobody authored on purpose is the failure permissions.md names:
    /// the save door refuses it in front of the author, and hydration refuses it again for
    /// the rows that arrive by some other road.</summary>
    Issue? OffAxis(string field, PolicyConstraint constraint, IReadOnlyList<string> keys)
    {
        // The members of a policy that declare the field agree on what it means, so the first
        // one that declares it answers for all of them (permissions.md, same field name =
        // same meaning). A field no member declares is the unbound case, reported above.
        if (keys.Select(key => FactoryFor(key).Fields).FirstOrDefault(fields => fields.ContainsKey(field)) is not { } declared)
        {
            return null;
        }

        var axis = declared[field];

        if (axis == RestrictAs.Flag)
        {
            if (constraint.Kind == PolicyConstraintKind.Any || constraint.Flag is not null)
            {
                return null;
            }

            return constraint.Kind == PolicyConstraintKind.Literal
                ? new Issue("ValuesOnFlag", $"'{field}' is a flag: it is restricted to true or false, never to a list of ids.", field)
                : new Issue("SymbolOnFlag", $"'{field}' is a flag, and no symbol relates one to the caller — restrict it to true or false, or leave it free.", field);
        }

        if (constraint.Flag is not null)
        {
            return new Issue("FlagOnValue", $"'{field}' is restricted by value, and true or false is not one of them.", field);
        }

        if (axis == RestrictAs.Reference && constraint.Kind is not (PolicyConstraintKind.Any or PolicyConstraintKind.Literal))
        {
            return new Issue("SymbolOnReference", $"'{field}' points at a catalog row, and no symbol relates one to the caller — restrict it to a list of values or leave it free.", field);
        }

        return null;
    }
}
