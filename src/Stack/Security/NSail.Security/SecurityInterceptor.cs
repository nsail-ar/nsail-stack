// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.Publishing;
using NSail.Problems;

namespace NSail.Security;

/// <summary>The gate: registered server-side as an open-generic Mediator interceptor,
/// it evaluates every send against the SecurityManager before the sender runs. Deny by
/// default: no matching policy means a refusal before any handler or DB work — 403 for a
/// caller who is somebody, 401 for one who is nobody (SecurityProblem.Denied). What a
/// published event's subscribers send in reaction is exempt: it is the system carrying out a
/// decision already authorized, never a caller asking (AmbientPublish).</summary>
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
        // publish and the sends behind them (AmbientPublish).
        if (AmbientPublish.Active)
        {
            await next(message, cancellationToken).ConfigureAwait(false);

            return;
        }

        var session = _sessions.Session;
        var result = await _security.Authorize(message, session, cancellationToken).ConfigureAwait(false);

        if (!result.Allowed)
            throw new BusinessException(SecurityProblem.Denied(session.IsAuthenticated, typeof(TMessage).Name));

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
        // Same exemption as the void arity: a subscriber reads before it writes, so a query it
        // sends in reaction is as much the system's as the command beside it.
        if (AmbientPublish.Active)
        {
            return await next(message, cancellationToken).ConfigureAwait(false);
        }

        var session = _sessions.Session;
        var result = await _security.Authorize(message, session, cancellationToken).ConfigureAwait(false);

        if (!result.Allowed)
            throw new BusinessException(SecurityProblem.Denied(session.IsAuthenticated, typeof(TMessage).Name));

        // Same handoff as the void arity: a query is where the org filter actually reads rows.
        return await AmbientAuthorization
            .Vetting(new AmbientGrant(result.FieldsVetted, result.EveryOrganization), () => next(message, cancellationToken))
            .ConfigureAwait(false);
    }
}
