// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Background;

/// <summary>The names the runner's own metrics carry: the meter a host subscribes, the three
/// instruments and the two attributes they are read by. They are public because they are a
/// contract with two readers outside this project — the telemetry setup, which subscribes the
/// meter, and the fleet's alert rules, which query the instruments by name.
///
/// <para>A metric and not the log lines beside it: the export seam drops every string attribute
/// off a log record (<c>LogScrubber</c>), so the line that names the job arrives at the backend
/// without the job in it. Nothing scrubs a metric's attributes, and an alert has to name what
/// broke.</para></summary>
public static class BackgroundJobMetrics
{
    public const string MeterName = "NSail.Background";

    /// <summary>Passes by job and outcome. One pass is one invocation of
    /// <see cref="IBackgroundJob.Run"/>, so a per-tenant job's tick adds one per tenant.</summary>
    public const string Passes = "nsail.background.job.passes";

    /// <summary>How many passes the job's last tick swept: the roster for a per-tenant job, one
    /// for an install-wide job and for an install that resolves no tenant at all, zero for the
    /// job that runs for nobody. Reported from the first tick, never before it.</summary>
    public const string Roster = "nsail.background.job.roster";

    /// <summary>How long since the job last completed a pass, counted in its own declared
    /// intervals: one dimensionless number, so a single rule covers every cadence and no rule
    /// has to know what any job's cadence is. It counts from boot until the first pass
    /// completes, so a job that runs for nobody is late too.</summary>
    public const string Overdue = "nsail.background.job.overdue";

    /// <summary>The job's type name. Not <c>job</c>: the Prometheus side of an OTLP gateway
    /// spends that label on the service itself, and a second one arriving under the same name
    /// collides with it.</summary>
    public const string JobTag = "nsail.job";

    /// <summary>The Mediator span's own attribute name and vocabulary, so one filter reads a
    /// job's outcome and an operation's. The constants cannot be shared: <c>MessageSpan</c>
    /// lives in NSail.Telemetry, which references this project and not the other way round.</summary>
    public const string OutcomeTag = "nsail.outcome";

    public const string Succeeded = "ok";

    public const string Failed = "failed";
}
