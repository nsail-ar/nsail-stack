// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Background;

/// <summary>The <c>Background</c> section: the budgets of the job runner and the deferred queue.</summary>
public class BackgroundOptions
{
    /// <summary>How long one pass of a job (one tenant's, for a per-tenant job) may run before
    /// the runner abandons it, unless <see cref="JobTimeouts"/> names the job.</summary>
    [Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]
    public TimeSpan JobTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Per-job override of <see cref="JobTimeout"/>, keyed by the job type's name.</summary>
    public Dictionary<string, TimeSpan> JobTimeouts { get; set; } = [];

    /// <summary>How long one deferred item may run before the queue stops waiting for it.</summary>
    [Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]
    public TimeSpan DeferredTimeout { get; set; } = TimeSpan.FromSeconds(30);

    internal TimeSpan TimeoutOf(string job)
    {
        return JobTimeouts.TryGetValue(job, out var timeout) ? timeout : JobTimeout;
    }

    // The cooperative window: the token is cancelled at the budget and the work gets this much
    // longer to honour it before the runner stops waiting.
    internal static TimeSpan Grace(TimeSpan budget)
    {
        var half = budget / 2;
        var cap = TimeSpan.FromSeconds(5);

        return half < cap ? half : cap;
    }
}
