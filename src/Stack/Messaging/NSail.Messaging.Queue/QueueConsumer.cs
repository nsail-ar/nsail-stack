// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.Sending;
using NSail.Serialization;

namespace NSail.Messaging.Queue;

/// <summary>One <c>[Queue]</c> message a composition knows, as the Queue.Consumers target
/// registers it. The drain delivers only what is registered here: the stored row's type name
/// picks an entry, never which type a string becomes.</summary>
public abstract class QueueConsumer
{
    public abstract string MessageType { get; }

    public abstract Task Deliver(
        IServiceProvider services,
        string body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken);
}

public sealed class QueueConsumer<TMessage> : QueueConsumer
    where TMessage : IMessage
{
    /// <summary>The full name of the type and nothing about its assembly: a row written before
    /// a deploy is still delivered after it.</summary>
    public static string Key { get; } = typeof(TMessage).FullName!;

    public override string MessageType
    {
        get { return Key; }
    }

    public override Task Deliver(
        IServiceProvider services,
        string body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var message = JsonSerializer.Deserialize<TMessage>(body, JsonOptions.Wire)
            ?? throw new InvalidOperationException($"The queued row for {Key} holds a body that reads as null, so there is no message to deliver.");

        // The in-process pipeline by name, the way a generated endpoint resolves its own: the
        // host's registered ISender for this message is the producer, and asking for it here
        // would store the row a second time instead of running the handler. Everything else
        // above the handler stands — the unit of work, the interceptors, the message's own
        // annotations and every rule — so a delivery is as atomic as the send that stored it.
        var pipeline = services.GetRequiredService<InProcessSendPipeline<TMessage>>();

        return AmbientQueue.Run(() => pipeline.Execute(message, headers, cancellationToken));
    }
}
