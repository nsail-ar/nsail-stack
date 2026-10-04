// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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
        var tasks = new Task[_handlers.Length + _subscriptions.Actions.Count];
        var tindex = 0;

        for (int i = 0; i < _handlers.Length; i++)
        {
            tasks[tindex] = _handlers[i].Handle(message, cancellationToken);
            tindex++;
        }

        var actions = _subscriptions.Actions;
        for (int i = 0; i < actions.Count; i++)
        {
            tasks[tindex] = (actions[i])(message, cancellationToken);
            tindex++;
        }
        
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}