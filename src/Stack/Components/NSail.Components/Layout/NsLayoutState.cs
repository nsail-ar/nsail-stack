// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

    public void ToggleDrawer()
    {
        DrawerIsOpen = !DrawerIsOpen;
    }
}