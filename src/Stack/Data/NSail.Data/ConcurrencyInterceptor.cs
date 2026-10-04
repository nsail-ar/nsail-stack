// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Messaging;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Problems;

namespace NSail.Data;

/// <summary>Turns a lost optimistic-concurrency race into the standard conflict Problem, for
/// every send a host with a DbContext serves. It sits in the pipeline rather than at the HTTP
/// edge because an in-process caller — a Blazor screen composed into the same host — never
/// passes through that edge and would otherwise meet the raw provider exception.</summary>
public sealed class ConcurrencyInterceptor<TMessage> : IInterceptor<TMessage>
    where TMessage : IMessage
{
    public async Task Invoke(TMessage message, PipelineDelegate<TMessage> next, CancellationToken cancellationToken)
    {
        try
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new BusinessException(await Concurrency.Describe(exception, cancellationToken).ConfigureAwait(false));
        }
    }
}

public sealed class ConcurrencyInterceptor<TMessage, TResult> : IInterceptor<TMessage, TResult>
    where TMessage : IMessage<TResult>
{
    public async Task<TResult> Invoke(TMessage message, PipelineDelegate<TMessage, TResult> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new BusinessException(await Concurrency.Describe(exception, cancellationToken).ConfigureAwait(false));
        }
    }
}
