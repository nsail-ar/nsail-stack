// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Runtime.ExceptionServices;

namespace NSail.Messaging.Runtime.Publishing;

public class InProcessPublisher<TMessage> : IPublisher<TMessage>
    where TMessage : IMessage
{
    readonly IHandler<TMessage>[] _handlers;
    readonly Subscriptions<TMessage> _subscriptions;

    public InProcessPublisher(
        IEnumerable<IHandler<TMessage>> handlers,
        Subscriptions<TMessage> subscriptions)
    {
        _handlers = handlers.ToArray();
        _subscriptions = subscriptions;
    }

    public async Task Publish(TMessage message, CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();

        for (int i = 0; i < _handlers.Length; i++)
        {
            var handler = _handlers[i];
            await Run(() => handler.Handle(message, cancellationToken), failures).ConfigureAwait(false);
        }

        var actions = _subscriptions.Actions;
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            await Run(() => action(message, cancellationToken), failures).ConfigureAwait(false);
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }

    static async Task Run(Func<Task> start, List<Exception> failures)
    {
        try
        {
            await start().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
    }
}
