// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Exporting;

/// <summary>One column of an <see cref="ExportTable"/>: the heading the reader sees and what the
/// values under it are. The format lives HERE and nowhere else — a value carrying its own would
/// be the same decision in two places, and a row that disagreed with its heading is a file no
/// spreadsheet can make sense of.
/// <para><see cref="Key"/> is the column's identity inside the table, not a word for anybody:
/// a writer that addresses columns by name (a per-column number format) keys on it, so two
/// columns translated to the same heading still carry their own format.</para></summary>
public sealed record ExportColumn
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public ExportFormat Format { get; init; }
}
