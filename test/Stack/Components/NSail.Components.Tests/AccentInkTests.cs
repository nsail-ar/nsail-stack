// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1940: the accent is a GROUND — a fill, a glyph, a slider — and the house used
/// to paint it as TEXT wherever something had to say "you are here". On a pale surface a free
/// pick reads 2.3:1 there, so the one word that marks where the reader is was the hardest word on
/// the screen to read. The answer is a theme token and not a rule per screen: the accent AS INK,
/// derived against every ground it lands on (BrandThemeContrastTests measures the readings), and
/// a second one for the chrome, which is a free pick of its own.
///
/// So what this file pins is the WIRING between the two halves — that the places which mark a
/// position read those tokens, and that nothing in the house paints the raw accent as text
/// again. bUnit paints nothing, so the class hooks come from a render and the declarations that
/// key on them from the stylesheet itself.</summary>
public sealed class AccentInkTests : BunitContext
{
    // The one slot that IS the accent at full strength. A rule that paints ink from it is the
    // defect, whatever surface it lands on: which ink reads on a free pick is a question about
    // that pick's luminance, and a stylesheet cannot ask it. The lookbehind is what keeps
    // background-color out — the accent as a GROUND is exactly what it is for.
    static readonly Regex AccentAsInk = new(
        @"(?<![-\w])color:\s*var\(--mud-palette-primary\)",
        RegexOptions.Compiled);

    // And the one rule that paints ink from it legitimately: .text-primary is NsImage's and
    // NsRemoteImage's As="Main" tint, which reaches an inlined SVG through currentColor. A glyph
    // is the other thing the accent is for (ui/branding.md), so a mark drawn in it is a mark at
    // the brand's own colour and not a word somebody has to read.
    const string ImageTint = ".text-primary";

    public AccentInkTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Directory.Party.Identity"] = "Identidad",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    // The whole family at once, and the reason this is a sweep of the stylesheet rather than a
    // list of selectors: the active tab, the open chapter, the wizard's current step, a primary
    // link and the focused field's own label were five rules that had each decided this
    // separately. A sixth is the next one to drift, so the invariant is "nowhere", not "not in
    // these five".
    [Fact]
    public void NoWordInTheHouseIsPaintedInTheRawAccent()
    {
        var offenders = Rules(Stylesheet())
            .Where(rule => AccentAsInk.IsMatch(rule.Body) && rule.Selector != ImageTint)
            .Select(rule => rule.Selector)
            .ToArray();

        Assert.Empty(offenders);
    }

    // The tab the reader is on. The selector the stylesheet carries is the vendor's own active
    // class under NsTabs' own root class, so both halves have to be true at once: the render says
    // the two classes meet on one element, and the stylesheet says what that pair is painted
    // with. The vendor's rule is .mud-tab.mud-tab-active and this one carries a third class, so
    // it out-ranks it without depending on which sheet the document loaded last.
    [Fact]
    public void TheTabTheReaderIsOnReadsTheAccentInk()
    {
        var cut = Render<TabTitleHost>();

        var active = cut.Find(".ns-tabs .mud-tabs-tabbar .mud-tab.mud-tab-active");

        Assert.Equal("Identidad", active.TextContent.Trim());
        Assert.Contains(
            ".ns-tabs .mud-tab.mud-tab-active {\n    color: var(--ns-accent-ink",
            Stylesheet());
    }

    // The guide's index marks the open chapter on two different grounds, because NsGuide renders
    // one list in two places: beside the content, where the ground is a card, and in the drawer's
    // gutter below the width that drawer docks at, where it is the CHROME. One token each —
    // NsGuideTests and NsGuideDrawerIndexTests are where the class on that row is pinned.
    [Fact]
    public void TheOpenChapterReadsACardsInkBesideTheContentAndTheBandsInsideTheDrawer()
    {
        var css = Stylesheet();

        Assert.Contains(
            ".ns-guide-entry-current {\n    background-color: var(--mud-palette-primary-hover);\n    color: var(--ns-accent-ink",
            css);

        Assert.Contains(
            ".ns-nav-drawer .ns-guide-entry-current {\n    background-color: var(--ns-rail-hover);\n    color: var(--ns-rail-accent);",
            css);
    }

    // The drawer's own entry, the sibling of the row above it in the same gutter: the two things
    // that say where the reader is read one pair, or one of them is marked in a hue the other is
    // not. The selector is long because the vendor's own carries five classes and this rule has to
    // out-rank it — at three it was losing outright, and a declaration that loses is a rule the
    // stylesheet states and the screen never shows (Optical's AccentInkTests is what saw that).
    [Fact]
    public void TheDrawerEntryTheReaderIsOnReadsTheBandsOwnAccentInk()
    {
        Assert.Contains(
            ".ns-nav-drawer.mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled) {"
                + "\n    background-color: var(--ns-rail-hover);\n    color: var(--ns-rail-accent);",
            Stylesheet());
    }

    // The band's token is the chrome's answer and the rail is where it becomes a rail word, the
    // same way every other slot in that block renames a theme value into the vocabulary the rules
    // under it read. The fallback is the band's own ink: a theme that never mounted leaves the
    // brand unmarked rather than unreadable.
    [Fact]
    public void TheRailNamesTheBandsAccentInkAndFallsBackToItsOwn()
    {
        Assert.Contains(
            "--ns-rail-accent: var(--ns-accent-on-chrome, var(--ns-rail-text));",
            Stylesheet());
    }

    // Both inks are luminance decisions, so the theme hands them over per lit scheme and the
    // stylesheet DEFINES neither: a blend left in :root would be the luminance-blind derivation
    // this replaced, still winning for any brand that never mounts a theme.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheThemeHandsOverBothInksForTheSchemeThatIsLit(bool dark)
    {
        var scheme = dark ? BrandTheme.DefaultDark() : BrandTheme.DefaultLight();

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, new Brand())
            .Add(x => x.Dark, dark));

        Assert.Contains($"--ns-accent-ink:{scheme.AccentInk};", cut.Markup);
        Assert.Contains($"--ns-accent-on-chrome:{scheme.AccentOnChrome};", cut.Markup);

        Assert.DoesNotContain("--ns-accent-ink:", Stylesheet());
        Assert.DoesNotContain("--ns-accent-on-chrome:", Stylesheet());
    }

    // The two are not one value under two names: the content's surfaces are fixed and the chrome
    // is picked, so the house's own dark scheme reads its card ink and its band ink as different
    // colours. A token that collapsed onto the other would be a card's answer painted on a band.
    [Fact]
    public void TheCardsInkAndTheBandsAreDerivedSeparately()
    {
        var dark = BrandTheme.DefaultDark();

        Assert.NotEqual(dark.AccentInk, dark.AccentOnChrome);
    }

    // Enough of a parse to name the selector a declaration belongs to, which is what makes a
    // failure here readable — "a word in .ns-guide-entry-current" rather than a line number.
    // Comments are stripped first: this file explains the accent at length, and the prose names
    // the very declaration it exists to forbid. A nested block (the container queries and the one
    // media query) has its own braces, so the selector of an inner rule comes out with its
    // wrapper's text in front of it — trimmed to the last line, which is the selector itself.
    static IEnumerable<(string Selector, string Body)> Rules(string css)
    {
        var stripped = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        foreach (var match in Regex.Matches(stripped, @"([^{}]*)\{([^{}]*)\}", RegexOptions.Singleline).Cast<Match>())
        {
            var selector = match.Groups[1].Value
                .Split('\n')
                .Last(line => !string.IsNullOrWhiteSpace(line))
                .Trim();

            yield return (selector, match.Groups[2].Value);
        }
    }

    static string Stylesheet()
    {
        return HouseStylesheet.Read();
    }
}
