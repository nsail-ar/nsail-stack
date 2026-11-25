// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;
using MudBlazor;

namespace NSail.Components;

// The vendor's open menu counts every row that registers with it and forgets none until it
// closes, and the arrows walk that count by position: once a search draws no row, the first
// positions are rows that are gone and Arrow Down focuses nothing. A row that leaves the menu
// therefore takes itself out of the count. A submenu stands in it twice, as its menu and as the
// row that opens it.
static class MenuRows
{
    // Private; MudVendorContractTests pins the names.
    static readonly FieldInfo? Rows =
        typeof(MudMenu).GetField("_menuItems", BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo? Registered =
        typeof(MudMenu).GetField("_registeredItems", BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo? Focused =
        typeof(MudMenu).GetField("_focusedIndex", BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo? Opener =
        typeof(MudMenu).GetField("_menuItemActivator", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void Forget(MudMenu? holder, MudMenuItem? row)
    {
        Remove(holder, row);
    }

    internal static void ForgetSubmenu(MudMenu? holder, MudMenu? submenu)
    {
        if (submenu is null)
        {
            return;
        }

        Remove(holder, Opener?.GetValue(submenu));
        Remove(holder, submenu);
    }

    static void Remove(MudMenu? holder, object? row)
    {
        if (holder is null || row is null || Rows?.GetValue(holder) is not List<object> rows)
        {
            return;
        }

        var index = rows.IndexOf(row);

        if (index < 0)
        {
            return;
        }

        rows.RemoveAt(index);

        (Registered?.GetValue(holder) as HashSet<object>)?.Remove(row);

        // The cursor's position shifts with the rows before it; on the row that left, it is
        // nowhere, and the next Arrow Down starts again from the first.
        if (Focused?.GetValue(holder) is int focused && focused >= index)
        {
            Focused.SetValue(holder, focused == index ? -1 : focused - 1);
        }
    }
}
