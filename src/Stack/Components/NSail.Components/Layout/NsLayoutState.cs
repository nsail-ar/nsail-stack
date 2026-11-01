// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

public class NsLayoutState
{
    bool _open;

    bool _summoned;

    public bool DrawerIsOpen
    {
        get => _open;
        set
        {
            if (_open != value)
            {
                _open = value;
                _summoned &= value;
                Changed?.Invoke();
            }
        }
    }

    /// <summary>Whether the person opened the drawer over the page. The layout starts the
    /// drawer open so the docked variant stands from the first frame, which on a phone is
    /// not a drawer over anything: only this says the sheet is up and covers the title row.</summary>
    public bool DrawerIsSummoned => _summoned;

    public event Action? Changed;

    /// <summary>Whether the layout renders an app bar. Read by end-anchored drawers (the aside
    /// surface) to decide whether they clip below it or run the full viewport height.</summary>
    public bool HasHeader { get; set; }

    RenderFragment? _brand;

    /// <summary>The install's brand as the nav drawer draws it, offered to the bar that stands
    /// in for the drawer while it is closed. Set by the drawer's menu.</summary>
    public RenderFragment? Brand
    {
        get => _brand;
        set
        {
            _brand = value;
            BrandChanged?.Invoke();
        }
    }

    public event Action? BrandChanged;

    public void ToggleDrawer()
    {
        if (_open)
        {
            DrawerIsOpen = false;
            return;
        }

        _summoned = true;
        DrawerIsOpen = true;
    }
}