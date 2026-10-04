// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using NSail.Messaging.Runtime.Publishing;
using NSail.Serialization;

namespace NSail.Messaging.WebApi.Push;

/// <summary>Publish's transport to the open clients of this scope's audience. Registered per
/// <c>[Pushed]</c> message by the SignalR.Hubs target, beside the in-process publisher: the
/// server's own handlers still hear the event, and the clients are one more audience.</summary>
public sealed class PushPublisher<TMessage> : IPublisher<TMessage>
    where TMessage : IMessage
{
    readonly IHubContext<PushHub> _hub;
    readonly PushAudience _audience;

    public PushPublisher(IHubContext<PushHub> hub, PushAudience audience)
    {
        _hub = hub;
        _audience = audience;
    }

    public async Task Publish(TMessage message, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(message, JsonOptions.Wire);

        await _hub.Clients
            .Group(_audience.Current)
            .SendAsync(PushFeed.Method, PushedMessage<TMessage>.Key, body, cancellationToken);
    }
}
