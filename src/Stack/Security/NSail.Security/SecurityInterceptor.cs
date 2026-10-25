// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.Publishing;
using NSail.Messaging.Runtime.Sending;
using NSail.Problems;

namespace NSail.Security;

/// <summary>The gate: registered server-side as an open-generic Mediator interceptor,
/// it evaluates every send against the SecurityManager before the sender runs. Deny by
/// default: no matching policy means a refusal before any handler or DB work — 403 for a
/// caller who is somebody, 401 for one who is nobody (SecurityProblem.Denied). What a
/// published event's subscribers send in reaction is exempt: it is the system carrying out a
/// decision already authorized, never a caller asking (AmbientPublish). A queued command's
/// delivery is exempt for the same reason, one hop later (AmbientQueue).</summary>
public sealed class SecurityInterceptor<TMessage> : IInterceptor<TMessage>
    where TMessage : IMessage
{
    readonly SecurityManager _security;
    readonly SessionProvider _sessions;

    public SecurityInterceptor(SecurityManager security, SessionProvider sessions)
    {
        _security = security;
        _sessions = sessions;
    }

    public async Task Invoke(TMessage message, PipelineDelegate<TMessage> next, CancellationToken cancellationToken)
    {
        // A reaction is not a request: the caller's grants answered the send that decided this
        // already, and asking again would make every policy name the events its messages
        // publish and the sends behind them (AmbientPublish). A delivery off the durable queue
        // is the same exemption across a wait: the send was answered "accepted" when it was
        // gated, and a grant revoked since would refuse here with nobody left to tell.
        if (AmbientPublish.Active || AmbientQueue.Active)
        {
            await next(message, cancellationToken).ConfigureAwait(false);

            return;
        }

        var session = _sessions.Session;
        var result = await _security.Authorize(message, session, cancellationToken).ConfigureAwait(false);

        if (!result.Allowed)
            throw new BusinessException(SecurityRefusal.For(result, session, typeof(TMessage).Name));

        // What allowed the send travels with it: the plumbing behind this gate acts on the
        // message's values and has no other way to learn whether a policy read them, nor how
        // wide the grant that allowed them reaches.
        await AmbientAuthorization
            .Vetting(new AmbientGrant(result.FieldsVetted, result.EveryOrganization), () => next(message, cancellationToken))
            .ConfigureAwait(false);
    }
}

public sealed class SecurityInterceptor<TMessage, TResult> : IInterceptor<TMessage, TResult>
    where TMessage : IMessage<TResult>
{
    readonly SecurityManager _security;
    readonly SessionProvider _sessions;

    public SecurityInterceptor(SecurityManager security, SessionProvider sessions)
    {
        _security = security;
        _sessions = sessions;
    }

    public async Task<TResult> Invoke(TMessage message, PipelineDelegate<TMessage, TResult> next, CancellationToken cancellationToken)
    {
        // Same exemptions as the void arity: a subscriber reads before it writes, so a query it
        // sends in reaction is as much the system's as the command beside it — and so is one a
        // queued command's handler sends while delivering.
        if (AmbientPublish.Active || AmbientQueue.Active)
        {
            return await next(message, cancellationToken).ConfigureAwait(false);
        }

        var session = _sessions.Session;
        var result = await _security.Authorize(message, session, cancellationToken).ConfigureAwait(false);

        if (!result.Allowed)
            throw new BusinessException(SecurityRefusal.For(result, session, typeof(TMessage).Name));

        // Same handoff as the void arity: a query is where the org filter actually reads rows.
        return await AmbientAuthorization
            .Vetting(new AmbientGrant(result.FieldsVetted, result.EveryOrganization), () => next(message, cancellationToken))
            .ConfigureAwait(false);
    }
}

/// <summary>Which refusal a denied verdict is, in one place for both arities: a feature the
/// install was not sold names itself, and everything else is the deny-by-default answer about
/// the caller. Two different facts, and a client that could not tell them apart would tell
/// somebody to ask for a permission nobody can grant them.</summary>
static class SecurityRefusal
{
    internal static Problem For(AuthorizationResult result, Session session, string action)
    {
        return result.FeatureOff is { } feature
            ? SecurityProblem.FeatureUnavailable(feature)
            : SecurityProblem.Denied(session.IsAuthenticated, action);
    }
}
