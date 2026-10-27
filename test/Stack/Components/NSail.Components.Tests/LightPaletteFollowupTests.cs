// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>nsail#2744's remaining findings past the accent-as-ink pass (<see
/// cref="LightInkRoutingTests"/>): a placeholder and a shortcut hint read at a fraction of the
/// text colour nobody tuned as one, the card edge's ring went invisible on a light surface, and
/// a plain info hint read as loud as a real warning. Each reads a scheme-aware token instead —
/// pinned here as the stylesheet actually reading it; what the token resolves to in each scheme
/// is <see cref="BrandThemeContrastTests"/> and <see cref="NsThemeSchemeVariablesTests"/>'s
/// half.</summary>
public sealed class LightPaletteFollowupTests
{
    [Fact]
    public void ThePlaceholderReadsTheSecondaryInkInsteadOfAFractionOfOne()
    {
        Assert.Contains(
            "::placeholder,\n"
                + ".mud-input > textarea.mud-input-root::placeholder,\n"
                + "div.mud-input-slot.mud-input-root::placeholder {\n"
                + "    color: var(--mud-palette-text-secondary);\n"
                + "    opacity: 1;",
            Stylesheet());
    }

    [Fact]
    public void TheSearchShortcutHintReadsTheSecondaryInkAndNotTheRefusedOne()
    {
        Assert.Contains(
            "color: var(--mud-palette-text-secondary);\n    font-size: .6875rem;",
            Stylesheet());
    }

    // The rule above carries no region of its own, and the same hint renders inside the rail
    // too (NsNavMenu's Map box): left unscoped, the house's light chrome under a dark rail
    // override would read 3.51:1, under the body floor the hint owes. This is the rail's own
    // counterpart, the same pattern .ns-text-muted already follows.
    [Fact]
    public void TheSearchShortcutHintOnTheRailReadsTheRailsMutedInk()
    {
        Assert.Contains(
            ".ns-nav-drawer .ns-search-key {\n    color: var(--ns-rail-text-muted);",
            Stylesheet());
    }

    // The card/popover elevation ring itself (BrandMudTheme.BuildElevations) is not in this
    // stylesheet at all -- Shadows is generated C#, pinned by
    // BrandMudThemeTests.EveryElevationLevelReadsTheSchemeAwareRingToken. What IS here is the
    // content aside's own box-shadow, written as a literal rule rather than an elevation level.
    [Fact]
    public void TheContentAsidesOwnRingReadsTheSameSchemeAwareToken()
    {
        Assert.Contains(
            "box-shadow: -3px 0 8px rgba(0, 0, 0, 0.18), 0 0 0 1px var(--ns-elevation-ring,",
            Stylesheet());
    }

    // The nav drawer's surface is Chrome, a free pick neither fixed scheme owns -- under the
    // house's own light chrome a literal ring of either colour is invisible (nsail#2744, round
    // 2), so the ring reads --ns-rail-edge instead, the same chrome-derived wash every other
    // hairline on the rail already reads.
    [Fact]
    public void TheNavDrawersOwnRingReadsTheRailsChromeDerivedEdge()
    {
        Assert.Contains(
            "box-shadow: 2px 0 6px rgba(0, 0, 0, 0.16), 0 0 0 1px var(--ns-rail-edge);",
            Stylesheet());
    }

    [Fact]
    public void AFilledInfoAlertReadsTheSchemeAwareTint()
    {
        Assert.Contains(
            ".mud-alert-filled-info {\n"
                + "    background-color: var(--ns-alert-info-bg, var(--mud-palette-info));\n"
                + "    border: 1px solid var(--ns-alert-info-border, transparent);\n"
                + "    color: var(--ns-alert-info-ink, var(--mud-palette-info-text)) !important;",
            Stylesheet());
    }

    static string Stylesheet()
    {
        return HouseStylesheet.Read();
    }
}
