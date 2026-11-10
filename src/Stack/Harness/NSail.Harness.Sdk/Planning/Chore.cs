// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// Bookkeeping the planner asks for beside the work: a stale lock off, a stage the board owes.
public sealed record Chore(ChoreKind Kind, int Story, string Value, string Why)
{
    // Planned by a keeper that takes a story with no open PR: written only while it still has none.
    public bool NoPr { get; init; }
}
