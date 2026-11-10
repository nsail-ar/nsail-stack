// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// What a pass was doing when it failed: the repository or board member it was calling, or its own
// planning between them.
public enum PassCall
{
    Planning,
    Statuses,
    Stories,
    Locks,
    Closed,
    Chores,
    Start,
}
