// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NSail.Background;

// What the runner says where a log line cannot be heard. The three instruments are the three
// ways a job goes quiet, one each, so the alert that fires names the reason and not just the
// job: a roster of zero is "runs for nobody", a failed pass is "fails", and an overdue count
// is "has not completed in far longer than its interval".
//
// A Meter nobody subscribes costs nothing — the same property the Mediator's ActivitySource
// already leans on — so the runner always holds one and no host configures anything.
sealed class JobMeter : IDisposable
{
    // Never swept, which is not the same number as swept nobody: a job whose first tick has
    // not landed yet reports no roster at all, so the alert on zero cannot fire on a boot.
    const int Unswept = -1;

    readonly Meter _meter = new(BackgroundJobMetrics.MeterName);
    readonly ConcurrentDictionary<string, Job> _jobs = new(StringComparer.Ordinal);
    readonly Counter<long> _passes;

    public JobMeter()
    {
        _passes = _meter.CreateCounter<long>(BackgroundJobMetrics.Passes, description: "Background job passes, by job and outcome.");

        // No unit on either gauge, deliberately: a unit is a suffix on the Prometheus side of
        // an OTLP gateway, so declaring one renames the series the alert rules were written
        // against. Both of them are counts of something the description names anyway.
        _meter.CreateObservableGauge(
            BackgroundJobMetrics.Roster,
            ObserveRoster,
            description: "Passes the job's last tick swept: the roster, one for install-wide work, zero for a job that runs for nobody.");

        _meter.CreateObservableGauge(
            BackgroundJobMetrics.Overdue,
            ObserveOverdue,
            description: "Time since the job last completed a pass, in its own declared intervals.");
    }

    public void Scheduled(string job, TimeSpan interval)
    {
        _jobs[job] = new Job(interval);
    }

    public void Swept(string job, int passes)
    {
        if (_jobs.TryGetValue(job, out var scheduled))
        {
            scheduled.Swept(passes);
        }
    }

    public void Passed(string job, bool succeeded)
    {
        _passes.Add(
            1,
            new KeyValuePair<string, object?>(BackgroundJobMetrics.JobTag, job),
            new KeyValuePair<string, object?>(BackgroundJobMetrics.OutcomeTag, succeeded ? BackgroundJobMetrics.Succeeded : BackgroundJobMetrics.Failed));

        if (succeeded && _jobs.TryGetValue(job, out var scheduled))
        {
            scheduled.Completed();
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    IEnumerable<Measurement<int>> ObserveRoster()
    {
        foreach (var (name, job) in _jobs)
        {
            if (job.Roster != Unswept)
            {
                yield return new Measurement<int>(job.Roster, Tag(name));
            }
        }
    }

    IEnumerable<Measurement<double>> ObserveOverdue()
    {
        foreach (var (name, job) in _jobs)
        {
            yield return new Measurement<double>(job.Overdue, Tag(name));
        }
    }

    static KeyValuePair<string, object?> Tag(string job)
    {
        return new KeyValuePair<string, object?>(BackgroundJobMetrics.JobTag, job);
    }

    // One job's standing state, read by the gauge callbacks on the exporter's thread and
    // written by the loop on its own — so both numbers move as a whole word and neither is
    // ever half-written.
    sealed class Job
    {
        readonly TimeSpan _interval;

        // Monotonic, not a wall clock: a machine whose time is corrected backwards would
        // otherwise report a job that just ran as overdue, or one that never ran as fresh.
        long _completed;

        int _roster = Unswept;

        public Job(TimeSpan interval)
        {
            _interval = interval;

            // Boot is the baseline, so a job that has never completed a pass is late from the
            // moment it was scheduled rather than invisible until its first success.
            _completed = Stopwatch.GetTimestamp();
        }

        public int Roster => Volatile.Read(ref _roster);

        public double Overdue => Stopwatch.GetElapsedTime(Volatile.Read(ref _completed)) / _interval;

        public void Swept(int passes)
        {
            Volatile.Write(ref _roster, passes);
        }

        public void Completed()
        {
            Volatile.Write(ref _completed, Stopwatch.GetTimestamp());
        }
    }
}
