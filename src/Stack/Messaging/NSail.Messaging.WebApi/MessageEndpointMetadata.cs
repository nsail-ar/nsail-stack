// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.WebApi;

/// <summary>Names the message a generated endpoint sends, carried as endpoint metadata so
/// it is readable from the matched route alone — before ASP.NET binds a body or a query
/// string, which is the only place a gate can answer ahead of a binding failure.</summary>
public sealed class MessageEndpointMetadata
{
    public MessageEndpointMetadata(Type messageType)
    {
        MessageType = messageType;
    }

    public Type MessageType { get; }
}
