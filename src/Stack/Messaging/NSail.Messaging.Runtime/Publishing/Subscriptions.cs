// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Publishing;

public delegate Task SubscriptionDelegate<TMessage>(TMessage message, CancellationToken cancellationToken);

public sealed class Subscriptions<TMessage>
    where TMessage : IMessage
{
    readonly object _lock = new();
    readonly List<SubscriptionDelegate<TMessage>> _actions = new();

    public IReadOnlyList<SubscriptionDelegate<TMessage>> Actions
    {
        get
        {
            lock (_lock)
            {
                return _actions.ToArray();
            }
        }
    }

    public void Add(SubscriptionDelegate<TMessage> action)
    {
        lock (_lock)
        {
            _actions.Add(action);
        }
    } 

    public void Remove(SubscriptionDelegate<TMessage> action)
    {
        lock (_lock)
        {
            _actions.Remove(action);
        }
    }
}