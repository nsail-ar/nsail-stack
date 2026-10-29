// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Queue;

/// <summary>The closed list the drain delivers from, keyed by the name the producer stored.
/// Built once rather than walked per row: a backlog is many rows of few types.</summary>
public sealed class QueueConsumers
{
    readonly Dictionary<string, QueueConsumer> _byType;

    public QueueConsumers(IEnumerable<QueueConsumer> consumers)
    {
        ArgumentNullException.ThrowIfNull(consumers);

        _byType = new Dictionary<string, QueueConsumer>(StringComparer.Ordinal);

        foreach (var consumer in consumers)
        {
            _byType[consumer.MessageType] = consumer;
        }
    }

    public QueueConsumer? For(string messageType)
    {
        return _byType.TryGetValue(messageType, out var consumer) ? consumer : null;
    }
}
