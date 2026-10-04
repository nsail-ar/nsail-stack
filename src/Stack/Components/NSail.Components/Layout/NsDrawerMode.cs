// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

public enum NsDrawerMode
{
    /// <summary>Always overlays the content, with a backdrop.</summary>
    Overlay,

    /// <summary>Docks beside the content on wide screens (pushing it); overlays on small screens.</summary>
    Docked,
}
