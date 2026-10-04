// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>One row's inline edit reaching its end. Cancel keeps the row open in edit mode —
/// so does a Problem the handler reports, which is how a rejected row stays where the user
/// can fix it instead of collapsing back to its display cells.</summary>
public sealed class RowEditEventArgs<TRow> : AsyncEventArgs
    where TRow : class
{
    public required TRow Row { get; init; }

    /// <summary>The row came from NewRow and has never been confirmed.</summary>
    public bool IsNew { get; init; }

    public bool Cancel { get; set; }
}
