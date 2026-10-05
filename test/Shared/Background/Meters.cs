// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;

namespace NSail.Background.Testing;

/// <summary>Reads the runner's own instruments the way the exporter does — by name, off the
/// meter, with the attributes attached. Every reading is filtered by the job's own name: the
/// meter's name is a constant, so a suite running another test class in parallel emits on a
/// meter of the same name, and only the job attribute tells the two apart.</summary>
public sealed class Meters : IDisposable
{
    readonly MeterListener _listener = new();
    readonly List<Reading> _readings = [];

    public Meters()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name is BackgroundJobMetrics.MeterName or DeferredWorkMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Record(instrument, measurement, tags));
        _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, _) => Record(instrument, measurement, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Record(instrument, measurement, tags));

        _listener.Start();
    }

    /// <summary>Asks every observable instrument for its value, which is the only way a gauge
    /// is ever read — the exporter does exactly this on its own interval.</summary>
    public void Poll()
    {
        _listener.RecordObservableInstruments();
    }

    /// <summary>What the counter has added for one job and outcome. A counter is a sum, so the
    /// readings are added rather than taken last.</summary>
    public int Passes(string job, string outcome)
    {
        lock (_readings)
        {
            return (int)_readings
                .Where(reading => reading.Instrument == BackgroundJobMetrics.Passes && reading.Job == job && reading.Outcome == outcome)
                .Sum(reading => reading.Value);
        }
    }

    /// <summary>The same sum for the deferred queue's own counter, keyed by the item's kind
    /// instead of a job's name — the one number an alert about a vendor send reads.</summary>
    public int Items(string kind, string outcome)
    {
        lock (_readings)
        {
            return (int)_readings
                .Where(reading => reading.Instrument == DeferredWorkMetrics.Completed && reading.Kind == kind && reading.Outcome == outcome)
                .Sum(reading => reading.Value);
        }
    }

    /// <summary>The last value a gauge answered for one job, or null when it has answered
    /// none — which is not the same as zero, and the difference is a job that has not ticked
    /// yet versus one that swept nobody.</summary>
    public double? Gauge(string instrument, string job)
    {
        lock (_readings)
        {
            return _readings
                .Where(reading => reading.Instrument == instrument && reading.Job == job)
                .Select(reading => (double?)reading.Value)
                .LastOrDefault();
        }
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        string? job = null;
        string? kind = null;
        string? outcome = null;

        foreach (var tag in tags)
        {
            if (tag.Key == BackgroundJobMetrics.JobTag)
            {
                job = tag.Value as string;
            }
            else if (tag.Key == DeferredWorkMetrics.KindTag)
            {
                kind = tag.Value as string;
            }
            else if (tag.Key == BackgroundJobMetrics.OutcomeTag)
            {
                outcome = tag.Value as string;
            }
        }

        lock (_readings)
        {
            _readings.Add(new Reading(instrument.Name, job, kind, outcome, value));
        }
    }

    sealed record Reading(string Instrument, string? Job, string? Kind, string? Outcome, double Value);
}
