// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;

namespace NSail.Messaging.Queue;

/// <summary>The drain's own signal. A singleton because a Meter is the process's, and the
/// counter is what an alert reads: nothing a queue drops is dropped silently.</summary>
public sealed class QueueMeter : IDisposable
{
    readonly Meter _meter = new(QueueMetrics.MeterName);
    readonly Counter<long> _delivered;

    public QueueMeter()
    {
        _delivered = _meter.CreateCounter<long>(
            QueueMetrics.Delivered,
            description: "Queued commands the drain finished with, by message and outcome.");
    }

    public void Finished(string messageType, string outcome)
    {
        _delivered.Add(
            1,
            new KeyValuePair<string, object?>(QueueMetrics.MessageTag, messageType),
            new KeyValuePair<string, object?>(QueueMetrics.OutcomeTag, outcome));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
