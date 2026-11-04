// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

public sealed record Plan(IReadOnlyList<WorkItem> Items, IReadOnlyList<Chore> Chores, IReadOnlyList<string> Notes);
