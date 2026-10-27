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
/// accent — what this file pins is that these four vendor rules actually read it).
///
/// A first pass left all four unscoped, so the band answered them the same way a card does —
/// a defect of its own (ui/branding.md, "On the chrome it is a second derivation"): AccentInk
/// is leaned against the content's fixed surfaces and a dark accent on a dark chrome can read
/// under 2:1 there. The chrome routing tests below are that correction: the four read
/// --ns-accent-on-chrome instead inside .mud-appbar/.ns-nav-drawer, the token
/// BrandThemeContrastTests.TheAccentOnTheChromeNeverReadsWorseThanTheBandsOwnInk already proves
/// never falls under the band's own ink there.</summary>
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

    // The same four, inside the chrome: each reads the band's own token instead of the card's,
    // scoped to .mud-appbar/.ns-nav-drawer with the extra class that is what wins it back from
    // the unscoped rule above — a specificity tie would leave the card's reading on the band.
    [Fact]
    public void TheTextButtonsOwnInkOnTheChromeIsTheAccentOnChrome()
    {
        Assert.Contains(
            ":is(.mud-appbar, .ns-nav-drawer) .mud-button-text.mud-button-text-primary {\n"
                + "    color: var(--ns-accent-on-chrome",
            Stylesheet());
    }

    [Fact]
    public void ThePrimaryTextUtilityOnTheChromeIsTheAccentOnChrome()
    {
        Assert.Contains(
            ":is(.mud-appbar, .ns-nav-drawer) .mud-primary-text {\n"
                + "    color: var(--ns-accent-on-chrome",
            Stylesheet());
    }

    [Fact]
    public void TheFocusedFieldsBorderOnTheChromeIsTheAccentOnChrome()
    {
        Assert.Contains(
            ":is(.mud-appbar, .ns-nav-drawer) .mud-input.mud-input-outlined:focus-within > .mud-input-outlined-border,\n"
                + ":is(.mud-appbar, .ns-nav-drawer) .mud-input.mud-input-outlined:focus-within .mud-shrink > .mud-input-outlined-border {\n"
                + "    border-color: var(--ns-accent-on-chrome",
            Stylesheet());
    }

    [Fact]
    public void TheKeyboardFocusRingOnTheChromeIsTheAccentOnChrome()
    {
        Assert.Contains(
            ":is(.mud-appbar, .ns-nav-drawer)\n"
                + ":is(a, button, [role=\"button\"], [role=\"tab\"], [role=\"menuitem\"], [role=\"option\"], summary, "
                + "[tabindex]:not([tabindex=\"-1\"])):focus-visible {\n"
                + "    outline: 2px solid var(--ns-accent-on-chrome",
            Stylesheet());
    }

    // The ring's content bucket no longer names the chrome, and names the aside drawer by its
    // own class instead of the vendor's shared one — the regression finding 1 named: .mud-drawer
    // matched .ns-drawer (content) and .ns-nav-drawer (chrome) alike, so narrowing the ring's
    // scope without widening .ns-drawer back in would have left the aside with no ring at all.
    [Fact]
    public void TheRingsContentBucketNamesTheAsideAndNeitherChromeClass()
    {
        Assert.Contains(
            ":is(.mud-main-content, .mud-dialog, .mud-popover, .ns-drawer)\n"
                + ":is(a, button, [role=\"button\"], [role=\"tab\"], [role=\"menuitem\"], [role=\"option\"], summary, "
                + "[tabindex]:not([tabindex=\"-1\"])):focus-visible {\n"
                + "    outline: 2px solid var(--ns-accent-ink",
            Stylesheet());
    }

    static string Stylesheet()
    {
        return HouseStylesheet.Read();
    }
}
