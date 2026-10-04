// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Components;

/// <summary>Maps a Breakpoint parameter to the container-width utilities in ns-mud.css.
/// Shared by the controls whose layout gives way on a narrow container.</summary>
static class NsResponsive
{
    /// <summary>Classes for a label that gives way to its icon on narrow containers:
    /// hidden by default, shown from the breakpoint up when the caller's own Breakpoint
    /// asks for it, unconditionally visible otherwise (no icon to collapse to, or the
    /// caller pinned it with Always). "ns-button-label" rides every case, pinned or not — it
    /// is the hook a container-scoped rule (the emphasis-ladder footer) collapses on its
    /// own terms, independent of what Breakpoint the caller happened to pass.</summary>
    public static string LabelClasses(Glyph? icon, NsBreakpoint breakpoint)
    {
        return Collapses(icon, breakpoint) ? $"ns-button-label d-c-none d-c-{Suffix(breakpoint)}-inline" : "ns-button-label";
    }

    /// <summary>Marker for the control itself, so the stylesheet can re-centre the icon
    /// once the label is gone (MudBlazor spaces the icon off the label it no longer has).</summary>
    public static string? CollapseClass(Glyph? icon, NsBreakpoint breakpoint)
    {
        return Collapses(icon, breakpoint) ? $"ns-collapse-{Suffix(breakpoint)}" : null;
    }

    /// <summary>Classes for a table cell that only earns its width from a container size up:
    /// gone by default, a cell again from the breakpoint on. Null pins the column on.
    /// <para>display:none rather than hidden so the remaining columns take the space back —
    /// and both utilities lose to the stacked layout's own rule, which is deliberate: a card
    /// has the vertical room the table did not.</para></summary>
    public static string? CellClasses(NsSize breakpoint)
    {
        return breakpoint == NsSize.None ? null : $"d-c-none d-c-{Suffix(breakpoint)}-table-cell";
    }

    /// <summary>Classes for chrome that answers to the WINDOW's width instead of its
    /// container's: gone below the given width, shown from it up. The one measurement that is
    /// not the surface's own, and it is narrow on purpose — the nav drawer docks or hides by
    /// the window, so chrome that stands in for the drawer while it is hidden has to flip on
    /// exactly that edge (styling.md, "Container queries").
    /// <para>Rides MudBlazor's own d-{bp}-* utilities, which ARE that vendor breakpoint's media
    /// query — the same number MudDrawer's Breakpoint docks at — so the two cannot drift.</para></summary>
    public static string ShownFromWindow(NsSize width)
    {
        return $"d-none d-{Suffix(width)}-flex";
    }

    /// <summary>The other face of ShownFromWindow: shown below the width, gone from it up. The
    /// pair is one decision written twice, so neither face is ever on screen with the other.</summary>
    public static string ShownBelowWindow(NsSize width)
    {
        return $"d-flex d-{Suffix(width)}-none";
    }

    /// <summary>Marker for a grid of side-by-side columns that keeps only its focused one
    /// below the breakpoint. Null keeps them all.
    /// <para>The stylesheet hides the rest rather than narrowing them: seven columns in the
    /// width of a drawer are not tight, they are unreadable.</para></summary>
    public static string? ColumnsClass(NsSize breakpoint)
    {
        return breakpoint == NsSize.None ? null : $"ns-time-collapse-{Suffix(breakpoint)}";
    }

    // No icon means nothing would be left to click, and the two ends of the enum are not
    // widths: Always is the caller pinning the label on, Never is a control whose label is
    // not coming back at any width — so neither writes a container query, and the icon-only
    // shape Never asks for is the control's own (ns-square), ungated.
    static bool Collapses(Glyph? icon, NsBreakpoint breakpoint)
    {
        return icon is not null && breakpoint is not (NsBreakpoint.Always or NsBreakpoint.Never);
    }

    static string Suffix(NsSize breakpoint)
    {
        return breakpoint switch
        {
            NsSize.Md => "md",
            NsSize.Lg => "lg",
            NsSize.Xl or NsSize.Xxl => "xl",
            _ => "sm"
        };
    }

    static string Suffix(NsBreakpoint breakpoint)
    {
        return breakpoint switch
        {
            NsBreakpoint.Md => "md",
            NsBreakpoint.Lg => "lg",
            NsBreakpoint.Xl or NsBreakpoint.Xxl => "xl",
            _ => "sm"
        };
    }
}
