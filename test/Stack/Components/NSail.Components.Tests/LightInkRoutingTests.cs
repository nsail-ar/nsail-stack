// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>nsail#2744: a UI sweep of the light palette found the raw accent painted as TEXT in
/// four more places the earlier accent-ink pass (nsail#1940, <see cref="AccentInkTests"/>) did
/// not reach, because none of the four is a house class — they are the vendor's own, read by a
/// screen that writes Color.Primary on a text button or an icon, or focuses a field. Each read
/// 2.3–2.4:1 for the house gold on a light surface, under the 3–4.5:1 the control owes
/// (<see cref="BrandThemeContrastTests.TheAccentInkClearsEveryGroundItIsPaintedOn"/> is the
/// arithmetic half: --ns-accent-ink itself already clears every ground in both schemes, for any
/// accent — what this file pins is that these four vendor rules actually read it).</summary>
public sealed class LightInkRoutingTests
{
    [Fact]
    public void TheTextButtonsOwnInkIsTheAccentAsInk()
    {
        Assert.Contains(
            ".mud-button-text.mud-button-text-primary {\n    color: var(--ns-accent-ink",
            Stylesheet());
    }

    [Fact]
    public void ThePrimaryTextUtilityIsTheAccentAsInk()
    {
        Assert.Contains(
            ".mud-primary-text {\n    color: var(--ns-accent-ink",
            Stylesheet());
    }

    [Fact]
    public void TheFocusedFieldsBorderIsTheAccentAsInk()
    {
        Assert.Contains(
            ".mud-input.mud-input-outlined:focus-within > .mud-input-outlined-border,\n"
                + ".mud-input.mud-input-outlined:focus-within .mud-shrink > .mud-input-outlined-border {\n"
                + "    border-color: var(--ns-accent-ink",
            Stylesheet());
    }

    [Fact]
    public void TheKeyboardFocusRingIsTheAccentAsInk()
    {
        Assert.Contains(
            ":focus-visible {\n    outline: 2px solid var(--ns-accent-ink",
            Stylesheet());
    }

    static string Stylesheet()
    {
        return HouseStylesheet.Read();
    }
}
