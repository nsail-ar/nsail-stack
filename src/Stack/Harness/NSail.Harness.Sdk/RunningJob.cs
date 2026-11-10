// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;

namespace NSail.Harness;

public sealed record RunningJob(string Id, WorkItem Item, int Slot, DateTimeOffset Started, string LogPath)
{
    // A build's steps, as Doing carries them.
    public const string WaitingForTurn = "waiting for its build turn";
    public const string Building = "building";
    public const string Testing = "testing";

    // What it is doing now, read off its log: an agent's last words (its own, not a verified state),
    // a build's step. Null where the log says nothing or cannot be read.
    public string? Doing { get; init; }

    // When its log last moved. An instant, not an age: a reader measures it against its own moment.
    public DateTimeOffset? LastWrote { get; init; }

    // How long its log may stay silent before the job reads as stale: the limit it already runs
    // under. Null where it is never stale (a build waiting for its turn) or the limit is unknown.
    public TimeSpan? SilenceLimit { get; init; }

    public bool StaleAt(DateTimeOffset at)
    {
        return LastWrote is { } wrote && SilenceLimit is { } limit && at - wrote > limit;
    }
}
