// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// Andon: the products whose failed cut holds the harness; while any is lit, Items is release work only.
public sealed record Plan(IReadOnlyList<WorkItem> Items, IReadOnlyList<Chore> Chores, IReadOnlyList<string> Notes)
{
    public IReadOnlyList<Andon> Andon { get; init; } = [];
}
