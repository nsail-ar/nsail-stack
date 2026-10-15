// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Exporting;

/// <summary>One row's values, positionally against the table's columns — the value a cell holds,
/// not its printed form: a writer is what turns a decimal into a cell, and one handed text has
/// nothing left to type. A row shorter than the columns leaves the rest of them empty.</summary>
public sealed record ExportRow
{
    public required IReadOnlyList<object?> Values { get; init; }

    public static ExportRow Of(params object?[] values)
    {
        return new ExportRow { Values = values };
    }
}
