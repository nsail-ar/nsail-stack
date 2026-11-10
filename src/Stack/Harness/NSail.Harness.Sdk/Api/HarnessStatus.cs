// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

// Where the harness stands: whether it starts work, what runs, and its last pass.
public sealed class HarnessStatus
{
    public required string Repository { get; init; }
    public required string Machine { get; init; }
    public string? Paused { get; init; }

    // Until when no agent starts: the agent's account refused work (its quota wall). Builds go on.
    public DateTimeOffset? AgentsOffUntil { get; init; }
    public MachineReadings? Readings { get; init; }
    public bool TakeNew { get; init; }
    public required IReadOnlyList<RunningJob> Running { get; init; }
    public Pass? Last { get; init; }
}
