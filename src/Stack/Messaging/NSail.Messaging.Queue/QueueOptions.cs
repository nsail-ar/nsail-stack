// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Queue;

/// <summary>Section <c>Queue</c>. Everything the delivery loop is judged by is here and not a
/// constant in a doc: an install that needs a longer backoff or one more attempt states it,
/// and the figures in these defaults are what an install that states nothing gets.</summary>
public class QueueOptions
{
    /// <summary>Which transport carries a queued command. Empty is the in-process one: the
    /// durable row is the queue, it sits in the install's own database, and the drain is a
    /// background job of this process — nothing is deployed that an install does not deploy
    /// today. The broker names below are declared and refused at startup until the brokers'
    /// own story lands, so an install that writes one is told rather than served silence.</summary>
    public string? Transport { get; set; }

    /// <summary>How many times a row is attempted before it is dead-lettered. Counted across
    /// passes and across restarts, because it is a column of the row and not a loop
    /// variable.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The wait before the first retry; each further attempt doubles it. A handler
    /// that keeps failing therefore costs its install a geometric and not a linear number of
    /// attempts on a vendor that is down.</summary>
    public TimeSpan Backoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How often the drain looks for due rows, per tenant.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How many due rows one pass delivers. A bound and not a target: the next pass
    /// is one Interval away, and a pass that read the whole table would hold one tenant's
    /// sweep for as long as its backlog takes.</summary>
    public int BatchSize { get; set; } = 50;
}
