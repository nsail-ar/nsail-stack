// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

// What a Harness posts to its Ops: who it is, how it stands, and the jobs that ended since its last
// report Ops accepted (null from a Harness that does not send them).
public sealed class HarnessReport
{
    public required string Machine { get; init; }
    public string? Version { get; init; }
    public required HarnessStatus Status { get; init; }
    public HarnessStatsView? Stats { get; init; }
    public IReadOnlyList<EndedJob>? Jobs { get; init; }
}
