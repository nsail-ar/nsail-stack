// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Components;

/// <summary>Puts the rows a table HOLDS in the order a pressed heading asked for. The values it
/// orders by are the ones the cells show — <see cref="TableColumn.Read"/>, so an enum orders by
/// its own word and a stamp in the reader's clock — because a list ordered by something other
/// than what is printed in the column reads as wrongly ordered (ui/hosts.md).
/// <para>A reader rather than the grid's own render, for the same reason <see cref="TableExport"/>
/// is one: the arithmetic — what an empty cell is worth, which comparison text gets — is settled
/// without a renderer.</para></summary>
public static class TableOrder
{
    /// <summary>The rows in the pressed column's order, blanks last whichever way it points: an
    /// empty cell is an unknown, not the smallest value there is. Equal values keep the order
    /// they arrived in, which is the order the list's own handler settled.</summary>
    public static IReadOnlyList<TRow> Sort<TRow>(IReadOnlyList<TRow> rows, TableColumn column, bool descending, CultureInfo culture)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(culture);

        // Read once per row, not once per comparison: the member is reached by reflection and a
        // comparison sort asks for the same cell several times over.
        var read = rows.Select(row => (Row: row, Value: column.Read(row))).ToList();

        var comparer = new CellComparer(culture);

        var filled = read.Where(cell => !IsBlank(cell.Value));

        return
        [
            .. (descending
                ? filled.OrderByDescending(cell => cell.Value, comparer)
                : filled.OrderBy(cell => cell.Value, comparer)).Select(cell => cell.Row),
            .. read.Where(cell => IsBlank(cell.Value)).Select(cell => cell.Row)
        ];
    }

    // What the column prints as nothing at all: no value, or a word with no letters in it. A cell
    // the screen fills with its own "sin backup" is not blank — it has text, and it orders by it.
    static bool IsBlank(object? value)
    {
        return value is null || (value is string text && string.IsNullOrWhiteSpace(text));
    }

    // Text compares the way the database that answered the list already collates it — case- and
    // accent-insensitively, in the reader's own culture (data.md): a list ordered by a stricter
    // rule than the read that produced it reads as wrong.
    sealed class CellComparer(CultureInfo culture) : IComparer<object?>
    {
        const CompareOptions Folded = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        public int Compare(object? x, object? y)
        {
            if (x is string left && y is string right)
            {
                return culture.CompareInfo.Compare(left, right, Folded);
            }

            // A number, a date, a span: its own type knows what larger means. Only between two
            // values of the SAME type — a column of one member cannot hold two, short of a
            // nullable cell, which is already blank and already gone.
            if (x is IComparable comparable && y is not null && x.GetType() == y.GetType())
            {
                return comparable.CompareTo(y);
            }

            return culture.CompareInfo.Compare(Text(x), Text(y), Folded);
        }

        string Text(object? value)
        {
            return Convert.ToString(value, culture) ?? string.Empty;
        }
    }
}
