// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Sending;

/// <summary>Transport adapter for Send: exactly one per message per host (HTTP on clients, in-process where the handler lives).</summary>
public interface ISender<TMessage>
    where TMessage : IMessage
{
    Task Send(TMessage message, CancellationToken cancellationToken);
}

/// <summary>Transport adapter for Send: exactly one per message per host (HTTP on clients, in-process where the handler lives).</summary>
public interface ISender<TMessage, TResult>
    where TMessage : IMessage<TResult>
{
    Task<TResult> Send(TMessage message, CancellationToken cancellationToken);
}
