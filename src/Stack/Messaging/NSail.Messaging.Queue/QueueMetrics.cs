// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Queue;

/// <summary>The names the drain's own metric carries — a metric and not a log line for the
/// reason <c>DeferredWorkMetrics</c> is: <c>LogScrubber</c> drops every string attribute off
/// an exported log record, so a line naming which message dead-lettered would arrive with the
/// name cut out of it.</summary>
public static class QueueMetrics
{
    public const string MeterName = "NSail.Messaging.Queue";

    /// <summary>One measurement per row the drain finished with, by message and outcome — the
    /// only question a durable queue has to answer for an alert: is anything failing or being
    /// dropped, and which message.</summary>
    public const string Delivered = "nsail.queue.delivered";

    public const string MessageTag = "nsail.queue.message";

    public const string OutcomeTag = NSail.Background.BackgroundJobMetrics.OutcomeTag;

    public const string Succeeded = NSail.Background.BackgroundJobMetrics.Succeeded;

    /// <summary>One attempt failed and the row is due again after its backoff.</summary>
    public const string Failed = NSail.Background.BackgroundJobMetrics.Failed;

    /// <summary>The row has failed <see cref="QueueOptions.MaxAttempts"/> times and will not
    /// be attempted again — kept apart from <see cref="Failed"/> because a retry that will
    /// come back is a different alert from work the install has stopped doing.</summary>
    public const string DeadLettered = "dead-lettered";
}
