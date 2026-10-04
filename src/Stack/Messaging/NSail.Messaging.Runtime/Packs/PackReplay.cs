// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Packs;

/// <summary>What a replay did: sent, already there, and the steps that refused. The caller
/// turns the errors into whatever its own surface speaks — the replayer names no Problem of
/// its own, because a pack's failure is the app's story to tell.</summary>
public sealed class PackReplay
{
    public int Imported { get; set; }

    public int Skipped { get; set; }

    public List<PackStepError> Errors { get; } = [];
}

public sealed class PackStepError
{
    public required int Index { get; init; }

    public required PackStep Step { get; init; }

    public required string Detail { get; init; }
}

/// <summary>What a failed step does to the rest of the replay.</summary>
public enum PackFailure
{
    /// <summary>Collect the step's error and keep going — every step is attempted.</summary>
    Collect,

    /// <summary>End the replay at the first failed step, leaving that one error.</summary>
    Stop,
}
