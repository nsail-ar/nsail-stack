// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

// What a Harness posts to its Ops: who it is and how it stands.
public sealed class HarnessReport
{
    public required string Machine { get; init; }
    public string? Version { get; init; }
    public required HarnessStatus Status { get; init; }
    public HarnessStatsView? Stats { get; init; }
}
