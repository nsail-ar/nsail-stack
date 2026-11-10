// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;
using NSail.Messaging;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Problems;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

/// <summary>The tenant-state wall for a send. <see cref="TenantStateMiddleware"/> stands on the
/// HTTP request, and an open Blazor circuit's sends never pass it again, so a tenant suspended
/// under one would keep working until its next document load. Asked here on every send of a
/// caller's, the circuit stops within the state memo's window
/// (<see cref="TenancyOptions.StateCacheSeconds"/>) with the same 503 the request wall answers.
/// A system session is exempt: background work has its own check at the runner.</summary>
public sealed class TenantStateInterceptor<TMessage> : IInterceptor<TMessage>
    where TMessage : IMessage
{
    readonly IServiceProvider _services;

    public TenantStateInterceptor(IServiceProvider services)
    {
        _services = services;
    }

    public async Task Invoke(TMessage message, PipelineDelegate<TMessage> next, CancellationToken cancellationToken)
    {
        await TenantStateWall.Guard(_services, cancellationToken).ConfigureAwait(false);
        await next(message, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class TenantStateInterceptor<TMessage, TResult> : IInterceptor<TMessage, TResult>
    where TMessage : IMessage<TResult>
{
    readonly IServiceProvider _services;

    public TenantStateInterceptor(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<TResult> Invoke(TMessage message, PipelineDelegate<TMessage, TResult> next, CancellationToken cancellationToken)
    {
        await TenantStateWall.Guard(_services, cancellationToken).ConfigureAwait(false);

        return await next(message, cancellationToken).ConfigureAwait(false);
    }
}

static class TenantStateWall
{
    // Resolved per send rather than injected, as the middleware beside it does: a host under
    // None registers no TenantStates, and a constructor dependency on it would fail the
    // container's own startup validation.
    internal static async Task Guard(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (services.GetService<SessionProvider>()?.Session is { IsSystem: true })
        {
            return;
        }

        if (services.GetService<TenantStates>() is not { } states)
        {
            return;
        }

        if (await states.Suspension(cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new BusinessException(TenancyProblem.Suspended());
        }
    }
}
