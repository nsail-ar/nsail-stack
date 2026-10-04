// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.UnitOfWork;
using NSail.Problems;

namespace NSail.Messaging.Runtime.Sending;

// The OUTERMOST in-process send runs in its own service scope, mirroring transport semantics (an
// HTTP request gets its own scope). This keeps scoped resources such as the DbContext from being
// shared by concurrent operations — e.g. two pages prerendering in the same request.
//
// A send NESTED in one joins that scope instead of taking another, so a composing handler and
// everything it sends share one DbContext, one connection and one transaction: the whole
// operation commits at the outermost success or leaves nothing behind. AmbientUnitOfWork owns
// both halves. The HTTP sender is untouched — a send that crosses a process boundary is a second
// operation by design.
//
// The two halves are separate on purpose. Run opens the unit and hands the operation's scope
// back to SendPipeline, which builds everything above the handler from it; Send then resolves
// the handler from the scope that built it. One order of operations, so an interceptor, a
// validator and the handler all read the same DbContext and are undone together.
public class InProcessSender<TMessage> : ISender<TMessage>, IAmbientSender
    where TMessage : IMessage
{
    readonly IServiceProvider _services;
    readonly IServiceScopeFactory _scopeFactory;

    public InProcessSender(IServiceProvider services, IServiceScopeFactory scopeFactory)
    {
        _services = services;
        _scopeFactory = scopeFactory;
    }

    public Task Run(
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        return AmbientUnitOfWork.Run(_scopeFactory, typeof(TMessage).Name, operation, cancellationToken);
    }

    public Task<TResult> Run<TResult>(
        Func<IServiceProvider, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        return AmbientUnitOfWork.Run(_scopeFactory, typeof(TMessage).Name, operation, cancellationToken);
    }

    public Task Send(TMessage message, CancellationToken cancellationToken)
    {
        return ResolveHandler(_services).Handle(message, cancellationToken);
    }

    static IHandler<TMessage> ResolveHandler(IServiceProvider services)
    {
        using var enumerator = services.GetServices<IHandler<TMessage>>().GetEnumerator();

        if (!enumerator.MoveNext())
            throw new BusinessException(MessagingProblem.HandlerNotFound<TMessage>());

        var handler = enumerator.Current;

        if (enumerator.MoveNext())
            throw new BusinessException(MessagingProblem.MultipleHandlers<TMessage>());

        return handler;
    }
}

public class InProcessSender<TMessage, TResult> : ISender<TMessage, TResult>, IAmbientSender
    where TMessage : IMessage<TResult>
{
    readonly IServiceProvider _services;
    readonly IServiceScopeFactory _scopeFactory;

    public InProcessSender(IServiceProvider services, IServiceScopeFactory scopeFactory)
    {
        _services = services;
        _scopeFactory = scopeFactory;
    }

    public Task Run(
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        return AmbientUnitOfWork.Run(_scopeFactory, typeof(TMessage).Name, operation, cancellationToken);
    }

    public Task<TOperationResult> Run<TOperationResult>(
        Func<IServiceProvider, CancellationToken, Task<TOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        return AmbientUnitOfWork.Run(_scopeFactory, typeof(TMessage).Name, operation, cancellationToken);
    }

    public Task<TResult> Send(TMessage message, CancellationToken cancellationToken)
    {
        return ResolveHandler(_services).Handle(message, cancellationToken);
    }

    static IHandler<TMessage, TResult> ResolveHandler(IServiceProvider services)
    {
        using var enumerator = services.GetServices<IHandler<TMessage, TResult>>().GetEnumerator();

        if (!enumerator.MoveNext())
            throw new BusinessException(MessagingProblem.HandlerNotFound<TMessage>());

        var handler = enumerator.Current;

        if (enumerator.MoveNext())
            throw new BusinessException(MessagingProblem.MultipleHandlers<TMessage>());

        return handler;
    }
}
