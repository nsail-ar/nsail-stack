// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>What one table's columns are: the per-column breakpoints, and the columns themselves
/// in the order the header declared them. NsTh declares, NsTd reads, and the two find each other
/// through the localization key both already derive from their For expression — so a column is
/// declared once, in the header, and its cells follow.
/// <para>Read during render, so it assumes the header renders before the body (it does: thead
/// precedes tbody in the tree) and that a column's breakpoint is markup, not state.</para>
/// <para>The ordered half is what an export is built from, and it is rebuilt on every header
/// render rather than accumulated: a column a list's own filter DROPS stops being declared, and a
/// set that only ever grew would put it in the file with nothing under it (ui/hosts.md).</para>
/// </summary>
public sealed class TableColumns
{
    readonly Dictionary<string, NsSize> _breakpoints = [];
    readonly List<TableColumn> _declared = [];

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

    /// <summary>Called by the table as it renders its header row, before the headers in it set
    /// their own parameters: it is the pass boundary, and the only thing that can tell this
    /// render's column set from the last one's. Nothing reads <see cref="Declared"/> during a
    /// render — only a gesture does — so the list standing momentarily empty is not observable.
    /// </summary>
    public void BeginPass()
    {
        _declared.Clear();
    }

    public void Declare(TableColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        _declared.Add(column);
    }

    /// <summary>The columns of the last header render, in header order: every column that names a
    /// field, and none of the ones that do not — the action column has nothing to put in a file
    /// and nothing to key on either (ui/hosts.md).</summary>
    public IReadOnlyList<TableColumn> Declared => _declared;
}
