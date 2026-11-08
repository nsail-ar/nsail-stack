// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;

namespace NSail.Harness;

// What an alert reads, as instruments and not log lines (a telemetry pipeline may cut a log's
// strings; a metric keeps its tags): every job that ends, by kind and outcome (ok, broke), every
// pass, by outcome (ok, failed) and a failed one's provider status, and how long the harness has stood paused.
public sealed class HarnessMetrics : IDisposable
{
    public const string MeterName = "NSail.Harness";

    readonly Meter _meter = new(MeterName);
    readonly Counter<long> _jobs;
    readonly Counter<long> _passes;
    Func<double> _paused = () => 0;

    public HarnessMetrics()
    {
        _jobs = _meter.CreateCounter<long>("harness.jobs", description: "Jobs that ended, by kind and outcome (ok, broke).");
        _passes = _meter.CreateCounter<long>("harness.passes", description: "Passes over the board, by outcome (ok, failed).");
        _meter.CreateObservableGauge("harness.paused", () => _paused(), unit: "s", description: "How long the harness has stood paused; 0 when it is not.");
    }

    public void JobEnded(string kind, bool broke)
    {
        _jobs.Add(1, new KeyValuePair<string, object?>("kind", kind), new KeyValuePair<string, object?>("outcome", broke ? "broke" : "ok"));
    }

    // `status`: the provider's code when a failed pass got one; a pass without it carries no such tag.
    public void Passed(bool failed, int? status = null)
    {
        var outcome = new KeyValuePair<string, object?>("outcome", failed ? "failed" : "ok");
        if (status is { } code)
        {
            _passes.Add(1, outcome, new KeyValuePair<string, object?>("status", code));
        }
        else
        {
            _passes.Add(1, outcome);
        }
    }

    public void PausedFor(Func<double> seconds)
    {
        _paused = seconds;
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
