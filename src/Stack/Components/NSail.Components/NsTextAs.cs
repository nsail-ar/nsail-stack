// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Semantic role for NsText — Title picks the heading treatment; add Subtitle/
/// Caption here if they show up, never as their own NsSize token.</summary>
public enum NsTextAs
{
    Body,
    Title,
    Subtitle,

    /// <summary>The SCREEN's own name: the same treatment as Title, drawn as a top-level
    /// heading and able to hold the cursor a navigation's arrival hands it (ui/focus.md). Only
    /// the title row that is the screen's subject takes it (NsTitleBar) — a section's row, a
    /// table's or an overlay's stays Title, because one row names the screen.</summary>
    PageName
}
