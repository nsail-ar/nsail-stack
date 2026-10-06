// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

    /// <summary>Keeps the row open with nothing drawn — so it fits only a "no" the person just
    /// gave, a declined confirmation. A rule the handler decided reports a Problem instead, which
    /// is the answer the row's own field draws. The difference is louder where the host settles
    /// its open row on the document's submit (NsListEditor): there a Cancel refuses that submit
    /// too, and a Guardar refused with nothing drawn is a Guardar that does nothing and says
    /// nothing.</summary>
    public bool Cancel { get; set; }
}
