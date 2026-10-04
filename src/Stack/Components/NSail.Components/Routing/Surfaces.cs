// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Named-surface catalog: the app's own browsing contexts, used as link targets
/// (an NsLink Target that is not a browser target names a surface). Products define their
/// own class for custom surfaces.</summary>
public static class Surfaces
{
    public static readonly Surface Aside = new("aside");
    public static readonly Surface Modal = new("modal");

    /// <summary>The dialog host's own surface (NsOpenDialog). It closes through its host
    /// rather than a query parameter, so nothing links to it — but it is still a surface,
    /// and naming it here is what keeps the host from spelling it itself.</summary>
    public static readonly Surface Dialog = new("dialog");

    /// <summary>Target vocabulary, never a surface name: one surface further out than the
    /// link renders in — main opens the aside, the aside opens the modal — so a shared
    /// component escalates without knowing which host it is rendered in.</summary>
    public static readonly Surface Auto = new("auto");

    /// <summary>Target vocabulary, never a surface name: the full page underneath, from
    /// anywhere. A null Target already means "navigate my own surface", so leaving an
    /// overlay needs its own word.</summary>
    public static readonly Surface Main = new("main");

    /// <summary>Browser target, never a surface name: HTML's own "_blank", handed to the
    /// browser instead of resolved by the app.</summary>
    public static readonly Surface Blank = new("_blank");
}
