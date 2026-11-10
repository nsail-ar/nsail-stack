// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

// What runs, the build turns, and the recent outcomes, newest first.
public sealed class JobsView
{
    public required IReadOnlyList<RunningJob> Running { get; init; }
    public required BuildsView Builds { get; init; }
    public required IReadOnlyList<JobRecord> Recent { get; init; }
}
