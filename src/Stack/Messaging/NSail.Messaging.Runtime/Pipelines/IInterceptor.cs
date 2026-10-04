// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Pipelines;

public interface IInterceptor<TMessage>
{
    public Task Invoke(TMessage message, PipelineDelegate<TMessage> next, CancellationToken cancellationToken);
}

public interface IInterceptor<TMessage, TResult> 
{
    public Task<TResult> Invoke(TMessage message, PipelineDelegate<TMessage, TResult> next, CancellationToken cancellationToken);
}