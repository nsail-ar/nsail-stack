// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging;

/// <summary>Handles a message. Send requires exactly one handler per message; Publish invokes all of them.</summary>
public interface IHandler<TMessage, TResult>
    where TMessage : IMessage<TResult>
{
    Task<TResult> Handle(TMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Handles a message. Send requires exactly one handler per message; Publish invokes all of them.</summary>
public interface IHandler<TMessage>
    where TMessage : IMessage
{
    Task Handle(TMessage message, CancellationToken cancellationToken = default);
}
