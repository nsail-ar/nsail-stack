// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Sending;
using NSail.Messaging.Runtime.Validation;
using NSail.Problems;

namespace NSail.Messaging.Runtime.Pipelines;

public class SendPipeline<TMessage> : IPipeline
    where TMessage : IMessage
{
    readonly IServiceProvider _services;
    readonly Func<IServiceProvider, ISender<TMessage>> _sender;

    public SendPipeline(IServiceProvider services)
        : this(services, Resolve)
    {
    }

    protected SendPipeline(IServiceProvider services, Func<IServiceProvider, ISender<TMessage>> sender)
    {
        _services = services;
        _sender = sender;
    }

    // Everything above the handler is built from the scope the operation runs in, never from the
    // scope that asked for the pipeline. On the outermost in-process send that scope does not
    // exist yet when the pipeline is resolved — the sender opens it. Building the interceptors,
    // the message's own DataAnnotations and every IValidator from the CALLER's scope instead
    // would run them outside the transaction the handler runs in: an interceptor's own write
    // would survive a refusal that undid the rest of the operation. Opening the unit first and
    // building from the scope it hands back puts all of them in one transaction. A nested send
    // arrives at the same place by a shorter road (the sender joins the ambient and hands back
    // the scope its caller already lives in), and a transport sender owns no scope at all, so its
    // pipeline keeps being built from the caller's — a process boundary is a second operation.
    //
    // The delivery's MessageContext wraps all of it, the sender's own scope included, so an
    // interceptor, a rule and the handler all read the headers this send was called with.
    public Task Execute(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        return AmbientMessageContext.Run(headers, () => Dispatch((TMessage)message, cancellationToken));
    }

    async Task Dispatch(TMessage typed, CancellationToken cancellationToken)
    {
        var sender = _sender(_services);

        if (sender is IAmbientSender ambient)
        {
            // Resolved twice on this path deliberately: the first answers whether this send owns
            // a scope, and only a sender built BY that scope hands the handler the DbContext the
            // transaction is on.
            await ambient.Run(
                (services, token) => Chain(services, _sender(services))(typed, token),
                cancellationToken).ConfigureAwait(false);

            return;
        }

        await Chain(_services, sender)(typed, cancellationToken).ConfigureAwait(false);
    }

    static ISender<TMessage> Resolve(IServiceProvider services)
    {
        using var enumerator = services.GetServices<ISender<TMessage>>().GetEnumerator();

        if (!enumerator.MoveNext())
            throw new BusinessException(MessagingProblem.SenderNotFound<TMessage>());

        var sender = enumerator.Current;

        if (enumerator.MoveNext())
            throw new BusinessException(MessagingProblem.MultipleSenders<TMessage>());

        return sender;
    }

    static PipelineDelegate<TMessage> Chain(IServiceProvider services, ISender<TMessage> sender)
    {
        PipelineDelegate<TMessage> next = sender.Send;

        // Validators sit innermost by construction rather than by registration order: an app
        // composes its rules wherever its own wiring runs, and they still answer after every
        // interceptor — including the security gate, so an unauthorized caller meets Forbidden
        // and never learns a rule's verdict nor pays for its query — and before the sender, so
        // no handler and no transport sees a message a rule refused.
        var rules = Materialize(services.GetServices<IValidator<TMessage>>());

        if (rules.Count > 0)
        {
            var send = next;

            next = async (message, cancellationToken) =>
            {
                await ValidatorChain.Guard(rules, message, cancellationToken).ConfigureAwait(false);
                await send(message, cancellationToken).ConfigureAwait(false);
            };
        }

        // The message's own DataAnnotations sit innermost too, and unconditionally — not a
        // registered IInterceptor: a registered ValidationInterceptor would run at the mercy of
        // AddMessaging's call order relative to AddSecurityEnforcement, so an anonymous
        // malformed send could answer 400 InvalidModel before the gate ever ran. A call
        // SendPipeline itself always makes, after every interceptor and before the business
        // rules above, is immune to Add*'s order the same way the validators above already
        // are: the gate answers first regardless of which host composes it or when.
        {
            var send = next;

            next = (message, cancellationToken) =>
            {
                MessageValidator.Guard(message);
                return send(message, cancellationToken);
            };
        }

        foreach (var interceptor in services.GetServices<IInterceptor<TMessage>>().Reverse())
        {
            var currentNext = next;
            next = (message, cancellationToken) => interceptor.Invoke(message, currentNext, cancellationToken);
        }

        return next;
    }

    static IReadOnlyList<IValidator<TMessage>> Materialize(IEnumerable<IValidator<TMessage>> validators)
    {
        return validators as IReadOnlyList<IValidator<TMessage>> ?? validators.ToArray();
    }
}

public class SendPipeline<TMessage, TResult> : IPipeline<TResult>
    where TMessage : IMessage<TResult>
{
    readonly IServiceProvider _services;
    readonly Func<IServiceProvider, ISender<TMessage, TResult>> _sender;

    public SendPipeline(IServiceProvider services)
        : this(services, Resolve)
    {
    }

    protected SendPipeline(IServiceProvider services, Func<IServiceProvider, ISender<TMessage, TResult>> sender)
    {
        _services = services;
        _sender = sender;
    }

    // Same order of operations as the void arity, and for the same reason — the delivery's
    // MessageContext included.
    public Task<TResult> Execute(IMessage<TResult> message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        return AmbientMessageContext.Run(headers, () => Dispatch((TMessage)message, cancellationToken));
    }

    async Task<TResult> Dispatch(TMessage typed, CancellationToken cancellationToken)
    {
        var sender = _sender(_services);

        if (sender is IAmbientSender ambient)
        {
            return await ambient.Run(
                (services, token) => Chain(services, _sender(services))(typed, token),
                cancellationToken).ConfigureAwait(false);
        }

        return await Chain(_services, sender)(typed, cancellationToken).ConfigureAwait(false);
    }

    static ISender<TMessage, TResult> Resolve(IServiceProvider services)
    {
        using var enumerator = services.GetServices<ISender<TMessage, TResult>>().GetEnumerator();

        if (!enumerator.MoveNext())
            throw new BusinessException(MessagingProblem.SenderNotFound<TMessage>());

        var sender = enumerator.Current;

        if (enumerator.MoveNext())
            throw new BusinessException(MessagingProblem.MultipleSenders<TMessage>());

        return sender;
    }

    static PipelineDelegate<TMessage, TResult> Chain(IServiceProvider services, ISender<TMessage, TResult> sender)
    {
        PipelineDelegate<TMessage, TResult> next = sender.Send;

        // Innermost for the same reasons as the void arity, and keyed on the message alone: a
        // save-time rule has nothing to say about what the operation returns, so an app writes
        // one IValidator<TMessage> whether or not the message carries a result.
        var rules = Materialize(services.GetServices<IValidator<TMessage>>());

        if (rules.Count > 0)
        {
            var send = next;

            next = async (message, cancellationToken) =>
            {
                await ValidatorChain.Guard(rules, message, cancellationToken).ConfigureAwait(false);

                return await send(message, cancellationToken).ConfigureAwait(false);
            };
        }

        // Same structural placement as the void arity: after every interceptor (the security
        // gate included, whichever order Add* ran in), before the business rules above.
        {
            var send = next;

            next = (message, cancellationToken) =>
            {
                MessageValidator.Guard(message);
                return send(message, cancellationToken);
            };
        }

        foreach (var interceptor in services.GetServices<IInterceptor<TMessage, TResult>>().Reverse())
        {
            var currentNext = next;
            next = (message, cancellationToken) => interceptor.Invoke(message, currentNext, cancellationToken);
        }

        return next;
    }

    static IReadOnlyList<IValidator<TMessage>> Materialize(IEnumerable<IValidator<TMessage>> validators)
    {
        return validators as IReadOnlyList<IValidator<TMessage>> ?? validators.ToArray();
    }
}
