// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Publishing;
using NSail.Storage.Settings;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Tests;

// Answers the screen's two reads with what the server would, and records who subscribed so a
// save published from a store's form reaches the list.
sealed class StoresMediator : Mediator
{
    readonly List<(Type Type, Func<IMessage, CancellationToken, Task> Handle)> _subscriptions = [];

    public StoresMediator()
        : base(new EmptyServices())
    {
    }

    public List<StorageStoreRow> Stores { get; set; } = [];

    public int Lists { get; private set; }

    public override Task<TResult> Send<TResult>(IMessage<TResult> message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        object answer = message switch
        {
            ListStorageStores => Listed(),
            LookupStorageProviders => new List<StorageProviderRef>
            {
                new() { Type = "file", DisplayName = "Local folder" },
                new() { Type = "s3", DisplayName = "S3-compatible", IsRemote = true },
            },
            _ => throw new InvalidOperationException($"Unexpected {message.GetType().Name}."),
        };

        return Task.FromResult((TResult)answer);
    }

    public override IDisposable Subscribe<TMessage>(SubscriptionDelegate<TMessage> callback)
    {
        var subscription = (typeof(TMessage), new Func<IMessage, CancellationToken, Task>((message, token) => callback((TMessage)message, token)));

        _subscriptions.Add(subscription);

        return new Unsubscribe(() => _subscriptions.Remove(subscription));
    }

    public override async Task Publish(IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken = default)
    {
        foreach (var (type, handle) in _subscriptions.ToArray())
        {
            if (type.IsInstanceOfType(message))
            {
                await handle(message, cancellationToken);
            }
        }
    }

    List<StorageStoreRow> Listed()
    {
        Lists++;

        return Stores.ToList();
    }

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
