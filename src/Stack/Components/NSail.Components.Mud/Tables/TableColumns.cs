// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The per-column breakpoints of one table. NsTh declares, NsTd reads, and the two
/// find each other through the localization key both already derive from their For expression
/// — so a column is declared once, in the header, and its cells follow.
/// <para>Read during render, so it assumes the header renders before the body (it does: thead
/// precedes tbody in the tree) and that a column's breakpoint is markup, not state.</para></summary>
public sealed class TableColumns
{
    readonly Dictionary<string, NsSize> _breakpoints = [];

    public void Declare(string key, NsSize breakpoint)
    {
        _breakpoints[key] = breakpoint;
    }

    /// <summary>The width a column appears from; None (never hidden) for a column that
    /// declared nothing — including the action column, which has no For to key on.</summary>
    public NsSize BreakpointFor(string? key)
    {
        if (key is null || !_breakpoints.TryGetValue(key, out var breakpoint))
        {
            return NsSize.None;
        }

        return breakpoint;
    }
}
