// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;

namespace NSail.Messaging.Queue.Tests;

/// <summary>Reads the drain's instrument the way the exporter does — by name, off the meter,
/// with its attributes attached. A counter is a sum, so the readings are added rather than
/// taken last.</summary>
public sealed class QueueReadings : IDisposable
{
    readonly MeterListener _listener = new();
    readonly List<(string Message, string Outcome, long Value)> _readings = [];

    public QueueReadings()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == QueueMetrics.MeterName && instrument.Name == QueueMetrics.Delivered)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            var message = Tag(tags, QueueMetrics.MessageTag);
            var outcome = Tag(tags, QueueMetrics.OutcomeTag);

            lock (_readings)
            {
                _readings.Add((message, outcome, measurement));
            }
        });

        _listener.Start();
    }

    public int Finished(string messageType, string outcome)
    {
        lock (_readings)
        {
            return (int)_readings
                .Where(reading => reading.Message == messageType && reading.Outcome == outcome)
                .Sum(reading => reading.Value);
        }
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    static string Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == name)
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
