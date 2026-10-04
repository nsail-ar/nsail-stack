// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Background;

/// <summary>The names <see cref="DeferredWorkRunner"/>'s own metric carries — a metric and not a
/// log line for the same reason <see cref="BackgroundJobMetrics"/> is: <c>LogScrubber</c> drops
/// every string attribute off an exported log record, so a line naming which deferred item
/// failed would arrive with the name cut out of it.</summary>
public static class DeferredWorkMetrics
{
    public const string MeterName = "NSail.Background.Deferred";

    /// <summary>One measurement per item the runner finished handling, by kind and outcome —
    /// the only question this queue has to answer for an alert: is anything failing, and
    /// which.</summary>
    public const string Completed = "nsail.deferred.work.completed";

    public const string KindTag = "nsail.deferred.kind";

    public const string OutcomeTag = BackgroundJobMetrics.OutcomeTag;

    public const string Succeeded = BackgroundJobMetrics.Succeeded;

    public const string Failed = BackgroundJobMetrics.Failed;

    /// <summary>The work ran longer than its budget and was abandoned — kept apart from
    /// <see cref="Failed"/> because a vendor that never answers is a different alert than one
    /// that answered no.</summary>
    public const string TimedOut = "timeout";
}
