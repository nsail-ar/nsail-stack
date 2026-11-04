// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Locks;

// Who holds a story: the machine, the job and its kind, and since when.
public sealed record LockHolder(string Machine, string Job, string Kind, DateTimeOffset At);
