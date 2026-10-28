// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Where a column or a cell is drawn once a table that opted into its compact phone
/// form (<c>NsTable Compact</c>) draws each row as one line: on both faces, left off that line,
/// or on that line only. A table that does not opt in never draws an Only cell and ignores the
/// rest.</summary>
public enum NsCompact
{
    Shown,

    /// <summary>Declared, and so a column of the exported file, but not drawn on the compact
    /// row — a number the row's own act already carries.</summary>
    Hidden,

    /// <summary>Drawn on the compact row and nowhere else, so the column and tablet faces read
    /// as they would without it.</summary>
    Only
}
