// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.SignalR;

/// <summary>Where the feed connects and over what. The address is the host's own origin plus
/// <see cref="PushFeed.Path"/>, so nothing about it is configured; what a composition may add
/// is the transport underneath — a test hands over its in-memory server's handler here.</summary>
public sealed class PushConnection
{
    readonly Uri _address;
    readonly Action<HttpConnectionOptions>? _transport;

    public PushConnection(Uri origin, Action<HttpConnectionOptions>? transport = null)
    {
        ArgumentNullException.ThrowIfNull(origin);

        _address = new Uri(origin, PushFeed.Path);
        _transport = transport;
    }

    public HubConnection Build(IRetryPolicy retry)
    {
        return new HubConnectionBuilder()
            .WithUrl(_address, options => _transport?.Invoke(options))
            .WithAutomaticReconnect(retry)
            .Build();
    }
}
