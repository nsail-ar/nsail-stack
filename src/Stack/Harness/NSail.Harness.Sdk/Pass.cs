// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;

namespace NSail.Harness;

// One read-plan-start round of the loop: what it planned, what fit, what it started, or why it failed.
public sealed record Pass(
    DateTimeOffset At,
    TimeSpan Took,
    Plan? Plan,
    IReadOnlyList<WorkItem> WouldTake,
    IReadOnlyList<string> Started,
    string? Error);
