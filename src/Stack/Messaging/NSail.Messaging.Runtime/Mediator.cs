// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.BclExtensions;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.Runtime;

public class Mediator
{
    readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider
            ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>Request/response: routes the message to its single sender (transport or in-process).</summary>
    public Task<TResult> Send<TResult>(IMessage<TResult> message, CancellationToken cancellationToken = default)
    {
        return Send(message, null, cancellationToken);
    }

    /// <summary>Send carrying headers for this call alone — the correlation id pattern. The
    /// headers are the delivery's, not the caller's: they reach every interceptor, rule and
    /// handler of THIS operation through an injected MessageContextAccessor and end with it.
    /// There is no ambient to write them into on purpose, so a header can never be attached to
    /// an operation by the order two calls happened to be made in.</summary>
    public virtual Task<TResult> Send<TResult>(
        IMessage<TResult> message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var pipelineType = typeof(SendPipeline<,>).MakeGenericType(message.GetType(), typeof(TResult));
        var pipeline = (IPipeline<TResult>)_serviceProvider.GetRequiredService(pipelineType);

        return pipeline.Execute(message, headers, cancellationToken);
    }

    /// <summary>Fire a command: routes the message to its single sender (transport or in-process).</summary>
    public Task Send(IMessage message, CancellationToken cancellationToken = default)
    {
        return Send(message, null, cancellationToken);
    }

    /// <summary>Send carrying headers for this call alone (see the result arity).</summary>
    public virtual Task Send(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var pipelineType = typeof(SendPipeline<>).MakeGenericType(message.GetType());
        var pipeline = (IPipeline)_serviceProvider.GetRequiredService(pipelineType);

        return pipeline.Execute(message, headers, cancellationToken);
    }

    /// <summary>Broadcast: delivers the message to every handler and subscription; zero listeners is valid.</summary>
    public Task Publish(IMessage message, CancellationToken cancellationToken = default)
    {
        return Publish(message, null, cancellationToken);
    }

    /// <summary>Publish carrying headers for this call alone (see Send's own). Every listener
    /// still hears the event — a header says who asked for it, never who may hear it.</summary>
    public virtual Task Publish(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var pipelineType = typeof(PublishPipeline<>).MakeGenericType(message.GetType());
        var pipeline = (IPipeline)_serviceProvider.GetRequiredService(pipelineType);

        return pipeline.Execute(message, headers, cancellationToken);
    }

    /// <summary>Listens for published messages until the returned IDisposable is disposed.</summary>
    public virtual IDisposable Subscribe<TMessage>(SubscriptionDelegate<TMessage> callback)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(callback);

        var subscriptions = _serviceProvider.GetRequiredService<Subscriptions<TMessage>>();
        subscriptions.Add(callback);

        return new DelegateDisposable(() =>
        {
            subscriptions.Remove(callback);
        });
    }
}