// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

// Per row, so it cannot live on TableColumns: that cascade is per table and fixed, and the
// flag has to distinguish one row from its neighbours.
sealed class TableRowEdit
{
    public static readonly TableRowEdit Open = new();
}
