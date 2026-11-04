// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// Bookkeeping the planner asks for beside the work: a stale lock off, a stage the board owes.
public sealed record Chore(ChoreKind Kind, int Story, string Value, string Why);
