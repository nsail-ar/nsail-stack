// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using NSail.Messaging.Runtime.Publishing;
using NSail.Serialization;

namespace NSail.Messaging.WebApi.Push;

/// <summary>Publish's road to the open clients of this scope's audience, over every transport
/// the host serves — the client picks one and the server cannot know which. Registered per
/// <c>[Pushed]</c> message by the Push.Publishers target, beside the in-process publisher: the
/// server's own handlers still hear the event, and the clients are one more audience.</summary>
public sealed class PushPublisher<TMessage> : IPublisher<TMessage>
    where TMessage : IMessage
{
    readonly IHubContext<PushHub> _hub;
    readonly PushStreams _streams;
    readonly PushAudience _audience;

    public PushPublisher(IHubContext<PushHub> hub, PushStreams streams, PushAudience audience)
    {
        _hub = hub;
        _streams = streams;
        _audience = audience;
    }

    public async Task Publish(TMessage message, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(message, JsonOptions.Wire);
        var audience = _audience.Current;

        _streams.Send(audience, PushedMessage<TMessage>.Key, body);

        await _hub.Clients
            .Group(audience)
            .SendAsync(PushFeed.Method, PushedMessage<TMessage>.Key, body, cancellationToken);
    }
}
