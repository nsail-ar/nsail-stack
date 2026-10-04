// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A <see cref="Mediator"/> that only records subscriptions, so a test can deliver
/// a published message to the component under test without standing up the send pipeline.</summary>
public sealed class CapturingMediator : Mediator
{
    readonly List<Subscription> _subscriptions = [];

    public CapturingMediator()
        : base(new EmptyServices())
    {
    }

    public override IDisposable Subscribe<TMessage>(SubscriptionDelegate<TMessage> callback)
    {
        var subscription = new Subscription(
            typeof(TMessage),
            (message, cancellationToken) => callback((TMessage)message, cancellationToken));

        _subscriptions.Add(subscription);

        return new Unsubscribe(() => _subscriptions.Remove(subscription));
    }

    // Delivered by the subscription's own type, the way InProcessPublisher does it: a double
    // that only ever knew one event type answered nothing for the next one, which reads as a
    // component that failed to subscribe.
    public override async Task Publish(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        foreach (var subscription in _subscriptions.ToArray())
        {
            if (subscription.Type.IsInstanceOfType(message))
            {
                await subscription.Handle(message, cancellationToken);
            }
        }
    }

    sealed record Subscription(Type Type, Func<IMessage, CancellationToken, Task> Handle);

    sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    sealed class Unsubscribe : IDisposable
    {
        readonly Action _action;

        public Unsubscribe(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action();
        }
    }
}
