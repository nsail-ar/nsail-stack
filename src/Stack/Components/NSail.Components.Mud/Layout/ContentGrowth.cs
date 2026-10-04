// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

// Whether the surface hosting this content hands its children a definite height to grow into.
// NsPanel's content always does; NsCard's does only when the card itself grows. A component
// that would otherwise declare a scroll of its own reads this instead of assuming: an
// overflow box nested in a chain that never resolves to a height does not scroll, it
// compresses — the content is then painted outside its own border, which reads as chopped
// rather than as cut off (the failure shape intentional-ui.md describes).
sealed class ContentGrowth
{
    public static readonly ContentGrowth Grows = new() { Grow = true };

    public static readonly ContentGrowth Natural = new() { Grow = false };

    public required bool Grow { get; init; }

    public static ContentGrowth For(bool grow)
    {
        return grow ? Grows : Natural;
    }
}
