// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Settings;

namespace NSail.Components;

/// <summary>What the prerender spent on the wire building the drawer, carried to the client
/// that replaces it. The arrangement rows rather than the arranged tree, and the numbers rather
/// than the entries they hang on: both are what an ask ANSWERED, and everything derived from
/// them is local work the client does again for free.</summary>
public sealed class NavMenuHandoff
{
    public IReadOnlyList<NavMenuArrangement> Arrangements { get; set; } = [];

    public IReadOnlyDictionary<string, int> Counts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
}
