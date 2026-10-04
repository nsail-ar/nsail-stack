// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Publishing;

/// <summary>Transport adapter for Publish: broadcast, all registered publishers run (in-process always included).</summary>
public interface IPublisher<TMessage>
    where TMessage : IMessage
{
    Task Publish(TMessage message, CancellationToken cancellationToken);
}
