// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Locks;

// Who holds a story: the machine, the harness process on it, the job and its kind, and since when.
public sealed record LockHolder(string Machine, string Job, string Kind, DateTimeOffset At, string Instance = "")
{
    public bool Is(LockHolder other)
    {
        return Machine == other.Machine && Instance == other.Instance && Job == other.Job;
    }
}
