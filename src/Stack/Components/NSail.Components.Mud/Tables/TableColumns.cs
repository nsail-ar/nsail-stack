// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>What one table's columns are: the per-column breakpoints and compact placements, and
/// the columns themselves in the order the header declared them. NsTh declares, NsTd reads, and
/// the two find each other through the localization key both already derive from their For
/// expression — so a column is declared once, in the header, and its cells follow.
/// <para>Read during render, so it assumes the header renders before the body (it does: thead
/// precedes tbody in the tree) and that a column's breakpoint is markup, not state.</para>
/// <para>The declared half is what an export is built from, and it is rebuilt on every header
/// render rather than accumulated: a column a list's own filter DROPS stops being declared, and a
/// set that only ever grew would put it in the file with nothing under it (ui/hosts.md).</para>
/// <para>It is also where the ORDER a pressed heading put the list in lives: the heading is what
/// takes the press and the table is what draws the rows, and this is the one thing the two
/// already share.</para>
/// </summary>
public sealed class TableColumns
{
    readonly Dictionary<string, NsSize> _breakpoints = [];
    readonly Dictionary<string, NsCompact> _compact = [];
    readonly List<TableColumn> _declared = [];

    public void Declare(string key, NsSize breakpoint)
    {
        Declare(key, breakpoint, NsCompact.Shown);
    }

    public void Declare(string key, NsSize breakpoint, NsCompact compact)
    {
        _breakpoints[key] = breakpoint;
        _compact[key] = compact;

        if (Lead is null && compact != NsCompact.Hidden)
        {
            Lead = key;
        }
    }

    /// <summary>Whether the table draws its compact phone form, said by the table itself so the
    /// cells under it can say which part of the line they are.</summary>
    public bool Compact { get; set; }

    /// <summary>The column a compact row opens with: the first one the header declared that the
    /// row draws. Every other surviving cell follows it on the row's second line.</summary>
    public string? Lead { get; private set; }

    /// <summary>Where a column is drawn on the compact row; Shown for a column that declared
    /// nothing.</summary>
    public NsCompact CompactFor(string? key)
    {
        if (key is null || !_compact.TryGetValue(key, out var compact))
        {
            return NsCompact.Shown;
        }

        return compact;
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
    /// render's column set from the last one's. What reads <see cref="Declared"/> reads a pass
    /// that has LANDED — a gesture, or the export act a surface announces, which waits for the
    /// pass where it finds nothing declared yet (NsTable, Declaring) — so the list standing
    /// momentarily empty between this call and the declarations that follow it is not
    /// observable.</summary>
    public void BeginPass()
    {
        _declared.Clear();
        Lead = null;
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

    /// <summary>Whether this table orders its rows at all, said by the table itself: only a grid
    /// holding every row it lists can, so a sortable heading in a server-paged or scrolling one
    /// draws no control (ui/hosts.md).</summary>
    public bool Orders { get; set; }

    /// <summary>The column the reader pressed, null while the list stands in the order it
    /// arrived in. The column itself and not its key: the press hands over the declaration it
    /// was made on, so nothing has to find it again in a set the next header render rebuilds.
    /// </summary>
    public TableColumn? Ordered { get; private set; }

    public bool Descending { get; private set; }

    /// <summary>Raised by a press, for the table to redraw its rows in: the heading pressed is a
    /// control of its own and a render of it alone would reorder nothing.</summary>
    public event Action? OrderChanged;

    /// <summary>A heading was pressed: ascending the first time, descending the second, and back
    /// to ascending — and another column takes the order over at ascending, one column at a
    /// time (ui/hosts.md).</summary>
    public void Press(TableColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        Descending = Ordered?.Key == column.Key && !Descending;
        Ordered = column;

        OrderChanged?.Invoke();
    }
}
