// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

public class NsLayoutState
{
    bool _open;

    public bool DrawerIsOpen
    {
        get => _open;
        set
        {
            if (_open != value)
            {
                _open = value;
                Changed?.Invoke();
            }
        }
    }

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
        DrawerIsOpen = !DrawerIsOpen;
    }
}