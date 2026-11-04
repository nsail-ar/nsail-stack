// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

public sealed class BuildsView
{
    public int Limit { get; init; }
    public required IReadOnlyList<BuildLease> Held { get; init; }
    public int Waiting { get; init; }
}
