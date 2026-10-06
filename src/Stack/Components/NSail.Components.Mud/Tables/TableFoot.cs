// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

// Not a flag on TableColumns, which is the whole table's: a cell has to tell the foot's row
// from a body row, and only the cascade around the footer fragment can say which it is in.
sealed class TableFoot
{
    public static readonly TableFoot Row = new();
}
