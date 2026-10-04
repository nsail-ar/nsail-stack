// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Serialization;

namespace NSail.Messaging.Runtime.Publishing;

/// <summary>One <c>[Pushed]</c> message a client composition knows, as the SignalR.Clients
/// target registers it. The feed publishes only what is registered here: the server's word
/// picks which entry, never which type a string becomes.</summary>
public abstract class PushedMessage
{
    /// <summary>The message's name on the wire, the same rule on both ends
    /// (<see cref="PushedMessage{TMessage}.Key"/>).</summary>
    public abstract string Name { get; }

    public abstract Task Publish(Mediator mediator, string body, CancellationToken cancellationToken);
}

public sealed class PushedMessage<TMessage> : PushedMessage
    where TMessage : IMessage
{
    /// <summary>The full name of the type and nothing about its assembly: a tab opened before a
    /// deploy still finds it, and the build bar is what says the server moved.</summary>
    public static string Key { get; } = typeof(TMessage).FullName!;

    public override string Name
    {
        get { return Key; }
    }

    public override async Task Publish(Mediator mediator, string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediator);

        if (JsonSerializer.Deserialize<TMessage>(body, JsonOptions.Wire) is { } message)
        {
            await mediator.Publish(message, cancellationToken);
        }
    }
}
