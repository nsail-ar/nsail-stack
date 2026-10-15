// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Exporting;

/// <summary>One sheet of the file, named: the tables that go on it, one under the other. A page
/// holds more than one table because a screen does — a list with its own totals, a report of
/// several registers — and a format that has no pages (CSV) is what flattens them.</summary>
public sealed record ExportPage
{
    public required string Name { get; init; }

    public required IReadOnlyList<ExportTable> Tables { get; init; }

    public static ExportPage Of(string name, ExportTable table)
    {
        return new ExportPage { Name = name, Tables = [table] };
    }
}
