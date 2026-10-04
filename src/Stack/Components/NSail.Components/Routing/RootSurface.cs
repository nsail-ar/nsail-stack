// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace NSail.Components;

/// <summary>
/// The default surface, as a service. Every SurfaceContext in the tree is built from the same
/// three things this one holds, so for the default surface the answer it gives is the answer
/// the cascade would have given — which is what lets a component that resolves an address
/// stop asking whether a cascade reached it. Named surfaces (aside, modal, dialog) still come
/// from the cascade alone: they are positions in a tree, and DI has no position.
/// </summary>
public sealed class RootSurface
{
    public RootSurface(
        NavigationManager navigation,
        RouteTable routeTable,
        IJSRuntime js,
        SurfaceHistory history)
    {
        Surface = new SurfaceContext(null, navigation, routeTable, js, history);
    }

    public SurfaceContext Surface { get; }
}
