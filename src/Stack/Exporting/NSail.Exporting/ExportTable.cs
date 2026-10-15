// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Exporting;

/// <summary>A grid of values with its headings: the unit a writer knows how to write, and the
/// only part of the model that is not in memory.
/// <para><see cref="Rows"/> is an <c>IAsyncEnumerable</c> because the rows of a real list arrive
/// a page at a time from somewhere that has to be awaited, and because a writer that pulls them
/// one at a time never holds the list: tens of thousands of rows cost the file and the row in
/// hand, not the result set. A source may therefore be enumerated ONCE — a writer that needed
/// two passes would ask a server for every page twice.</para></summary>
public sealed record ExportTable
{
    public string? Name { get; init; }

    public required IReadOnlyList<ExportColumn> Columns { get; init; }

    public required IAsyncEnumerable<ExportRow> Rows { get; init; }
}
