// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using MudBlazor;
using MudBlazor.Utilities;
using NSail.Components;
using NSail.Tones;

namespace NSail.Components.Tests;

/// <summary>The palette is legible, and the arithmetic says so rather than an eye (nsail#842).
/// A scheme is free to move — that is the whole point of one palette in one place — so the
/// floor moves with it: body text clears 4.5:1 against every ground it is drawn on and the
/// quieted rank clears 3:1 as well, in both schemes, or the change does not land.</summary>
public sealed class BrandThemeContrastTests
{
    // WCAG 2.2's own two numbers. Which one binds a dashboard figure is not a free choice: the
    // large-text line sits at 18pt/24px for regular weight, and only drops to 14pt/18.66px for
    // bold. A card's figure is body-sized (ui/styling.md, nsail#1272) — Typo.body1's 16px at
    // weight 400 — so it clears neither line and the body floor is the one that binds; the
    // body-floor test below reads TextSecondary on every ground, which is that figure at its
    // quietest. The large floor is kept and still tested, one theory down.
    const double BodyFloor = 4.5;
    const double LargeFloor = 3;

    // NsTheme's own light-only tint for a filled info alert (--ns-alert-info-bg): dark restates
    // the vendor's flat fill and moves nothing, so only light mixes the surface toward Info.
    const double AlertInfoTintWeight = 0.12;

    // MudBlazor's own hover share of the primary (--mud-palette-primary-hover), the wash a washed
    // row is marked with: the active tab's own hover, the wizard's current step, the guide's open
    // chapter. Named here and nowhere in BrandTheme because the derivation does not measure it —
    // it lies between a bare rung and the tenth below it, which the ink clears at both ends — so
    // the one place it has to appear is the test that checks the claim.
    const double HoverWeight = 0.06;

    // Main-vs-aside's step, held from ABOVE in both schemes and from BELOW in dark (the floor
    // below): a tone close enough that a component tuned on Background reads the same on Surface,
    // never a tonal gulf elevation alone should be carrying. The readings today are 1.05:1 in
    // light (Background vs the white Surface/SurfaceRaised sit at) and 1.05:1 in dark (Background
    // vs Surface) — the same scale in both — with dark's raised rung a touch further at 1.11:1.
    // Light owes no floor of its own: white is as far as Surface can rise, so there is nowhere
    // left for the step to collapse to.
    const double LightStepCeiling = 1.08;

    // The most OKLCH chroma the light canvas may carry under a chrome a colour field cannot
    // refuse — nsail#2335, where the house's own SurfaceTint read on a canvas mask that still had
    // gamut to spare (unlike the near-white card masks, which already back their own chroma off)
    // and turned `#3b7091` into a sky-blue page. Loose enough that a genuinely steel chrome still
    // tints the canvas a hair, tight enough that it never reads as a coloured page again.
    const double LightCanvasChromaCeiling = 0.01;

    // Each scheme as it ships, and worn under chromes a colour field cannot refuse: the ladder
    // takes the chrome's hue (BrandTone.Tint), so a floor met on the default warm greys has to
    // be met on a navy, a steel, a neon and a black band's greys too. The tint holds each rung's
    // luminance, which is why one set of floors serves them all.
    public static TheoryData<string, BrandTheme> Schemes()
    {
        var schemes = new TheoryData<string, BrandTheme>
        {
            { "dark", BrandTheme.DefaultDark() },
            { "light", BrandTheme.DefaultLight() },
        };

        foreach (var chrome in new[] { "#0b1626", "#3b7091", "#1565c0", "#000000", "#ffffff", "#f6ff00", "#c62828", "#2e7d32", "#7b1fa2" })
        {
            schemes.Add($"dark {chrome}", Worn(BrandTheme.DefaultDark(), chrome));
            schemes.Add($"light {chrome}", Worn(BrandTheme.DefaultLight(), chrome));
        }

        return schemes;
    }

    // As a save lays a pick down: the chrome and the two inks derived from it (BrandTone.Ink).
    // The chrome's own ink is a separate promise, made for the house chrome only: over a
    // mid-luminance pick neither ink clears the floor (ui/branding.md), which no ladder can fix.
    public static TheoryData<string, BrandTheme> HouseSchemes => new()
    {
        { "dark", BrandTheme.DefaultDark() },
        { "light", BrandTheme.DefaultLight() },
    };

    static BrandTheme Worn(BrandTheme theme, string chrome)
    {
        var ink = BrandTone.Ink(chrome);

        theme.Chrome = chrome;
        theme.TextOnChrome = ink.Primary;
        theme.TextOnChromeSecondary = ink.Secondary;

        return theme;
    }

    // Every filled face there is: four severities in each scheme, each with the fill the vendor
    // paints and the ink the palette hands it. Read off the palette, so a slot left unset shows
    // up here as the vendor's own white.
    public static TheoryData<string, string, string, string> Faces()
    {
        var faces = new TheoryData<string, string, string, string>();

        foreach (var (scheme, theme) in new[] { ("dark", BrandTheme.DefaultDark()), ("light", BrandTheme.DefaultLight()) })
        {
            var palette = Palette(theme);

            faces.Add(scheme, "error", theme.Error, palette.ErrorContrastText.Value);
            faces.Add(scheme, "warning", theme.Warning, palette.WarningContrastText.Value);
            faces.Add(scheme, "success", theme.Success, palette.SuccessContrastText.Value);
            faces.Add(scheme, "info", theme.Info, palette.InfoContrastText.Value);
        }

        return faces;
    }

    // Picks a colour field cannot refuse — CreateBranding constrains the accent to a length and
    // nothing else — each of them in both schemes. The neon is Óptica Lúmina's, the pastels are
    // the case the flat blend this replaced could not read.
    public static TheoryData<string, BrandTheme, string> Picks()
    {
        var picks = new TheoryData<string, BrandTheme, string>();

        foreach (var accent in new[] { "#F68E1E", "#000000", "#ffffff", "#f6ff00", "#f7c9d5", "#bfe3d0", "#2a3f84" })
        {
            picks.Add("dark", BrandTheme.DefaultDark(), accent);
            picks.Add("light", BrandTheme.DefaultLight(), accent);
        }

        return picks;
    }

    // Every rung of the ladder, THE CANVAS INCLUDED. A card and a raised sheet are the obvious
    // two, and the canvas was left out of this sweep as "what those stand on, not something text
    // is set on" — which three screens disproved at once: NsPanel paints nothing of its own
    // (--ns-panel-surface IS the canvas, ui/styling.md), so a chromeless panel's NsText is read
    // on Background and on nothing else. That is the +/-% figure in Ajustes de Venta, the
    // paragraph on Contraseña and the Totales label of Sumas y Saldos, all three filed at 4.25:1
    // against the light canvas of the day. Sheets alone is how a rank under the floor shipped.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void BodyTextClearsTheBodyFloorOnEveryGroundItIsDrawnOn(string scheme, BrandTheme theme)
    {
        foreach (var ground in new[] { theme.Background, theme.Surface, theme.SurfaceRaised })
        {
            Clears(theme.TextPrimary, ground, BodyFloor, scheme);
            Clears(theme.TextSecondary, ground, BodyFloor, scheme);
        }
    }

    // The muted rank is where a zero-valued KPI lands, so it is the figure with the least
    // contrast on the board. The body floor above is what binds it and already covers it on
    // every ground; this keeps nsail#842's own criterion on the record, so a figure that ever
    // grows back to a heading's size is still held to something here.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void AQuietedFigureClearsTheLargeFloor(string scheme, BrandTheme theme)
    {
        Clears(theme.TextSecondary, theme.Surface, LargeFloor, scheme);
    }

    // The third rung, and the only one with a CEILING. Body text and the muted rank owe a floor
    // because they are there to be read; a refused control owes both — it has to stay legible,
    // or the user cannot tell WHICH button is waiting on them, and it has to stay visibly under
    // the muted rank, or "you cannot press this" reads as ordinary quiet text.
    // Measured on the surfaces rather than on the fill under a disabled BUTTON, because most of
    // what wears this ink carries no fill at all — a greyed glyph, a menu row, a dimmed link —
    // and the button's own ground is the same surface washed by a further 6%, which moves the
    // reading by about four hundredths.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void TheRefusedInkIsLegibleAndStillUnMistakableForLiveText(string scheme, BrandTheme theme)
    {
        foreach (var surface in new[] { theme.Surface, theme.SurfaceRaised })
        {
            Assert.True(
                Ratio(theme.TextDisabled, surface) >= 2,
                $"{scheme}: {Reading("the refused ink", theme.TextDisabled, surface)} is not legible at all");

            Assert.True(
                Ratio(theme.TextDisabled, surface) < LargeFloor,
                $"{scheme}: {Reading("the refused ink", theme.TextDisabled, surface)} reads as live text");

            Assert.True(
                Ratio(theme.TextDisabled, surface) < Ratio(theme.TextSecondary, surface),
                $"{scheme}: the refused ink is not quieter than the muted rank on {surface}");
        }
    }

    // A severity at FULL strength is a band like the two below, not a surface, so its text is
    // read against the fill and never against the card the fill sits on. Measured off the
    // palette the vendor is handed rather than off BrandTheme, because the defect was an unset
    // slot: the constants were right and the vendor's own white reached the screen.
    [Theory]
    [MemberData(nameof(Faces))]
    public void AFilledSeverityFaceIsReadInItsOwnInk(string scheme, string severity, string fill, string ink)
    {
        Clears(ink, fill, BodyFloor, $"{scheme} {severity}");
    }

    // The status channel's neutral — Severity = null, a LIVE state — stands on a card, so what
    // binds it is the body floor and not the refused ink's ceiling. ns-mud.css points
    // .ns-status-muted at --mud-palette-text-secondary (NsStatusToneTests reads the
    // declaration); this is the other half, what that variable resolves to and whether a word
    // painted in it can be read on the surface it is listed on.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void TheNeutralStatusWordClearsTheBodyFloorOnTheCardItIsListedOn(string scheme, BrandTheme theme)
    {
        var palette = Palette(theme);

        foreach (var surface in new[] { theme.Surface, theme.SurfaceRaised })
        {
            Clears(palette.TextSecondary.Value, surface, BodyFloor, scheme);
        }
    }

    // And it stays the quiet one by being ACHROMATIC rather than by reading lower: on the light
    // card the neutral word reads 5.65:1 and the Warning one 5.49:1, so a contrast ordering has
    // nothing to say here at all. What makes a neutral state read as neutral is that it carries
    // no hue, which here means it is leaned out of the ink ladder's muted rung and out of none
    // of the four severities.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void TheNeutralStatusWordIsTheMutedRankAndNeverASeverity(string scheme, BrandTheme theme)
    {
        var palette = Palette(theme);

        Assert.Equal(new MudColor(theme.TextSecondary).Value, palette.TextSecondary.Value);

        foreach (var severity in new[] { theme.Error, theme.Warning, theme.Success, theme.Info })
        {
            Assert.NotEqual(new MudColor(severity).Value, palette.TextSecondary.Value);
        }
    }

    // nsail#1935, the half of it arithmetic can take: every reading of the status channel against
    // every ground a row gives it. A toned word starts from a severity, and a severity is a FILL
    // — chosen so near-black or white reads on top of it (the theory above) — which as 16px of
    // text on a card met nothing: light Warning read 3.32:1 and light Info 3.70:1, and dark Error
    // 4.05:1 on a raised sheet. So the stylesheet leans each one into the scheme's own ink, and
    // the neutral a tenth of the way into it for the same reason, and both are graded here.
    //
    // The GROUNDS are the whole point, and the pointer is in them: a status word is read in a
    // table row, NsTable hovers every row it draws, and the vendor answers a hover by tinting the
    // row — so every ink in this file is read on a ground a tenth darker in light and lighter in
    // dark than the surface it was tuned against. The sweep is a cross product and not a pairing
    // of each word with its own row: an order that is Listo and overdue at once wears Success on
    // a danger-washed row, and the two kit grids that print a toned state carry no wash at all,
    // which is the plain pointed row and the worst ground of the lot. That the classes carry
    // these mixes is read off the stylesheet in NsStatusToneTests; what they RESOLVE to is this.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void EveryStatusWordClearsTheBodyFloorOnEveryGroundARowGivesIt(string scheme, BrandTheme theme)
    {
        var severities = new[] { theme.Error, theme.Warning, theme.Success, theme.Info };

        var grounds = new List<string> { theme.Surface, theme.SurfaceRaised, PointedRow(theme) };
        var words = new List<string> { BrandTone.Mix(theme.TextPrimary, theme.TextSecondary, BrandTone.NeutralLean) };

        foreach (var severity in severities)
        {
            grounds.Add(RowWash(severity, theme));
            grounds.Add(PointedWash(severity, theme));
            words.Add(BrandTone.Mix(theme.TextPrimary, severity, BrandTone.ToneLean));
        }

        foreach (var word in words)
        {
            foreach (var ground in grounds)
            {
                Clears(word, ground, BodyFloor, scheme);
            }
        }
    }

    // The lean is the LARGEST that clears, which is what keeps the hue: a fraction tuned for the
    // floor alone would drift toward the plain ink one story at a time until the four tones were
    // four greys. The ceiling is the next step up failing on the worst ground above, so moving
    // the lean has to move this number in the open — at 0.70 light Warning falls to 4.26:1 on a
    // pointed row, which is where it was found.
    [Fact]
    public void TheLeanIsNoDeeperThanTheFloorNeeds()
    {
        var theme = BrandTheme.DefaultLight();
        var shallower = BrandTone.Mix(theme.TextPrimary, theme.Warning, BrandTone.ToneLean + 0.05);

        Assert.True(
            Ratio(shallower, PointedRow(theme)) < BodyFloor,
            $"a lean of {BrandTone.ToneLean + 0.05:0.00} would clear the floor too, so the lean is deeper than it needs to be");
    }

    // nsail#2744: an outlined field's border at rest is a boundary, not a decoration, so it owes
    // the large floor the table/paper hairlines above do not — left at the vendor's own default
    // it read 1.79:1 on the light canvas, and the mask that first gave dark a rung of its own
    // repeated Lines' dark value and read 1.30:1 there, the same defect transplanted to the other
    // scheme. Swept across the chrome cube like every other rung, and in BOTH schemes: the mask
    // is tinted the same way they are, so the floor cannot be met in one and skipped in the
    // other.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void TheInputsLineClearsTheLargeFloorOnTheSurface(string scheme, BrandTheme theme)
    {
        Clears(theme.LinesInputs, theme.Surface, LargeFloor, scheme);
    }

    // The two rungs diverge on purpose in both schemes: the decorative Lines stays a hairline
    // (table rows, an outlined paper) while LinesInputs is the one line in the house dark enough
    // to read as a boundary — darker than Lines against the near-white light surface, lighter
    // than Lines against the near-black dark one. Darkening (or, in dark, dimming) Lines itself
    // to the same floor is the mistake this keeps off the record.
    [Fact]
    public void TheInputsLineIsFartherFromTheSurfaceThanTheDecorativeLinesInBothSchemes()
    {
        var light = BrandTheme.DefaultLight();
        var dark = BrandTheme.DefaultDark();

        Assert.True(
            Ratio(light.LinesInputs, light.Surface) > Ratio(light.Lines, light.Surface),
            $"LinesInputs ({Ratio(light.LinesInputs, light.Surface):0.00}:1) is not darker than Lines ({Ratio(light.Lines, light.Surface):0.00}:1) in light");

        Assert.True(
            Ratio(dark.LinesInputs, dark.Surface) > Ratio(dark.Lines, dark.Surface),
            $"LinesInputs ({Ratio(dark.LinesInputs, dark.Surface):0.00}:1) is not lighter than Lines ({Ratio(dark.Lines, dark.Surface):0.00}:1) in dark");
    }

    // The light-only tint NsTheme emits for a filled info alert: TextPrimary read on the card
    // surface mixed a tenth-plus toward Info, which is the face the hint actually stands on and
    // not the raw fill AFilledSeverityFaceIsReadInItsOwnInk grades. Light only -- dark restates
    // the vendor's flat fill, already covered by that theory.
    [Fact]
    public void TheInfoAlertsLightOnlyTintIsReadInTheCardsOwnInk()
    {
        var light = BrandTheme.DefaultLight();
        var tint = BrandTone.Mix(light.Surface, light.Info, AlertInfoTintWeight);

        Clears(light.TextPrimary, tint, BodyFloor, "light info tint");
    }

    // The two bands that are not the content's surface, each read in its own ink: the accent a
    // filled button wears, and the chrome the appbar and the drawer wear.
    [Theory]
    [MemberData(nameof(HouseSchemes))]
    public void TheAccentAndTheChromeAreReadInTheirOwnInk(string scheme, BrandTheme theme)
    {
        Clears(theme.TextOnAccent, theme.Accent, BodyFloor, scheme);
        Clears(theme.TextOnChrome, theme.Chrome, BodyFloor, scheme);
        Clears(theme.TextOnChromeSecondary, theme.Chrome, BodyFloor, scheme);
    }

    // The accent AS INK, on every ground the house paints it on: the tonal rung's label, a link,
    // the active tab, the guide chapter the reader is on. The ink and the washed grounds are BOTH
    // functions of the same free pick, which is how a flat blend of the two used to converge on
    // itself: 55% of a pastel over the light scheme read 3.3:1 on its own wash, and 55% of black
    // over the dark one read 2.97:1.
    [Theory]
    [MemberData(nameof(Picks))]
    public void TheAccentInkClearsEveryGroundItIsPaintedOn(string scheme, BrandTheme theme, string accent)
    {
        theme.Accent = accent;

        foreach (var ground in Grounds(theme))
        {
            Clears(theme.AccentInk, ground, BodyFloor, scheme);
        }
    }

    // The theory above is the picks worth naming; this is the promise they stand for. A colour
    // field takes any of 16 million, so the floor is swept rather than sampled — coarsely, but
    // across the whole cube and in both schemes, because an accent that reads on the dark wash
    // is exactly the one that can vanish on the light one.
    [Fact]
    public void NoAccentAColourFieldAllowsLeavesTheAccentInkIllegible()
    {
        foreach (var theme in new[] { BrandTheme.DefaultDark(), BrandTheme.DefaultLight() })
        {
            for (var red = 0; red <= 255; red += 51)
            {
                for (var green = 0; green <= 255; green += 51)
                {
                    for (var blue = 0; blue <= 255; blue += 51)
                    {
                        theme.Accent = $"#{red:x2}{green:x2}{blue:x2}";

                        foreach (var ground in Grounds(theme))
                        {
                            Clears(theme.AccentInk, ground, BodyFloor, theme.Accent);
                        }
                    }
                }
            }
        }
    }

    // What the browser was looking at when the rung was signed off. The house accent is one lean
    // step short of the full lean in light, which is what widening the floor from the soft wash
    // alone to every ground the ink is painted on costs the default brand — one step of
    // saturation on a label, paid for by a tab strip and a guide index that read 2.3:1 and 1.9:1
    // before (nsail#1940). Dark never had to move: the ink is near-white there and the canvas,
    // the card and the raised sheet all sit well under it.
    [Fact]
    public void TheHouseBrandsAccentInkIsPinned()
    {
        Assert.Equal("#efb575", BrandTheme.DefaultDark().AccentInk);
        Assert.Equal("#8e5a21", BrandTheme.DefaultLight().AccentInk);
    }

    // The band's own accent ink, which answers to two free picks rather than one — the accent
    // AND the chrome it is read on. A floor cannot be promised over that square, because a
    // chrome whose own ink does not clear it (a mid-luminance pick, BrandTone.Ink's inversion
    // threshold) has nothing legible to lean from in the first place; what IS promised is that
    // marking the row never costs the reader anything the band's plain ink was already giving
    // them.
    [Theory]
    [MemberData(nameof(Picks))]
    public void TheAccentOnTheChromeNeverReadsWorseThanTheBandsOwnInk(string scheme, BrandTheme theme, string accent)
    {
        theme.Accent = accent;

        foreach (var ground in new[] { theme.Chrome, RailWash(theme) })
        {
            Assert.True(
                Ratio(theme.AccentOnChrome, ground) >= Math.Min(BodyFloor, Ratio(theme.TextOnChrome, ground)),
                $"{scheme}: {Reading("the accent on the chrome", theme.AccentOnChrome, ground)} is under the band's own ink");
        }
    }

    // And over the chromes the house itself ships, where the band's ink does clear, the mark that
    // says "you are here" clears the body floor outright — on the bare band and on the wash the
    // drawer paints under that one row.
    [Theory]
    [MemberData(nameof(HouseSchemes))]
    public void TheAccentOnTheHouseChromeClearsTheBodyFloor(string scheme, BrandTheme theme)
    {
        Clears(theme.AccentOnChrome, theme.Chrome, BodyFloor, scheme);
        Clears(theme.AccentOnChrome, RailWash(theme), BodyFloor, scheme);
    }

    // The canvas sinking BELOW the surfaces is what both schemes owe, and both now owe a step
    // big enough to read on its own — close to the same reading, so a component tuned on the
    // canvas reads the same standing on a surface in either scheme (ui/branding.md's
    // inherit-the-ground rule). GentleStepFloor is BELOW the ceiling the next test holds light
    // to: the two together are the "perceptible but not destructive" band this story opened to
    // land in, light included, where it used to be no step at all.
    const double GentleStepFloor = 1.03;

    [Fact]
    public void TheCanvasSinksBelowTheSurfacesInBothSchemes()
    {
        var dark = BrandTheme.DefaultDark();
        var light = BrandTheme.DefaultLight();

        Assert.True(Luminance(dark.Background) < Luminance(dark.Surface));
        Assert.True(Luminance(dark.Surface) < Luminance(dark.SurfaceRaised));
        Assert.True(Luminance(light.Background) < Luminance(light.Surface));

        Assert.True(
            Ratio(dark.Surface, dark.Background) >= GentleStepFloor,
            Reading("dark card", dark.Surface, dark.Background));
    }

    // The order above says the canvas is under the surfaces; this says how far under, on BOTH
    // sides of the band — gentle enough that neither scheme reads as a hard edge of tone or as
    // two different pages, and "gentle" is a number a run keeps rather than a screenshot
    // somebody once approved. Dark's Surface sits in the same band as light's; its raised rung
    // is allowed a touch further (a dialog floats OVER a surface, not beside one).
    [Fact]
    public void TheCanvasToSurfaceStepStaysGentleInBothSchemes()
    {
        var dark = BrandTheme.DefaultDark();
        var light = BrandTheme.DefaultLight();

        Assert.True(
            Ratio(dark.Surface, dark.Background) <= LightStepCeiling,
            $"{Reading("dark card", dark.Surface, dark.Background)} > {LightStepCeiling}");

        Assert.True(
            Ratio(light.Surface, light.Background) <= LightStepCeiling,
            $"{Reading("light card", light.Surface, light.Background)} > {LightStepCeiling}");

        Assert.True(
            Ratio(light.SurfaceRaised, light.Background) <= LightStepCeiling,
            $"{Reading("light raised card", light.SurfaceRaised, light.Background)} > {LightStepCeiling}");
    }

    // nsail#2335: `BrandTone.Tint` only caps a rung's chroma, never its luminance — a mask with
    // gamut to spare rides that cap at full strength regardless of how pale it reads. The light
    // canvas's mask is tuned as close to white as the cards' own, so it backs its chroma off the
    // same way; this is what keeps it there under a steel chrome a colour field cannot refuse.
    [Fact]
    public void TheLightCanvasStaysAchromaticUnderAVividChrome()
    {
        var light = Worn(BrandTheme.DefaultLight(), "#3b7091");

        Assert.True(
            Chroma(light.Background) <= LightCanvasChromaCeiling,
            $"the light canvas {light.Background} carries {Chroma(light.Background):0.0000} OKLCH chroma > {LightCanvasChromaCeiling}");
    }

    // What the eye is given, not what the rule says. Every ground the accent's ink lands on: the
    // three rungs bare, each of them under the accent's own soft wash — --ns-accent-soft is mixed
    // against transparent, so NsAs.Important's label stands on the wash over whichever rung the
    // button sits on — and each under the vendor's 6% hover, the ground the active tab and the
    // open chapter are marked with. The mixing is BrandTone's own (it is arithmetic, and a second
    // lerp here would only be a second way to be wrong); the reading is this file's, so the
    // derivation is never graded by its own contrast maths.
    static IEnumerable<string> Grounds(BrandTheme theme)
    {
        foreach (var rung in new[] { theme.Background, theme.Surface, theme.SurfaceRaised })
        {
            yield return rung;
            yield return BrandTone.Mix(rung, theme.Accent, HoverWeight);
            yield return BrandTone.Mix(rung, theme.Accent, BrandTone.WashWeight);
        }
    }

    // The wash the drawer marks one row with: the band's own ink at --ns-rail-hover's share of it,
    // composited over the chrome, which is what the entry the reader is on actually stands on.
    static string RailWash(BrandTheme theme)
    {
        return BrandTone.Mix(theme.Chrome, theme.TextOnChrome, BrandTone.RailWeight);
    }

    // The ground a status word stands on where both channels of the attention semaphore overlap:
    // the Órdenes de Trabajo row, where the CONDITION paints the row (.ns-row-*, ns-mud.css) and
    // the state paints the word, so a warning word is read on a warning-washed row. The fraction
    // is the stylesheet's own (BrandTone.RowWash) for the same reason Wash composes
    // --ns-accent-soft above: a ground has to be composable by whatever measures the label
    // standing on it.
    static string RowWash(string severity, BrandTheme theme)
    {
        return BrandTone.Mix(theme.Surface, severity, BrandTone.RowWash);
    }

    // The same row UNDER THE POINTER, which is when a person is reading it. The wash deepens in
    // its own tone there rather than being replaced by the vendor's neutral tint (ns-mud.css
    // hands --mud-palette-table-hover this value per washed row), so the condition channel
    // survives the hover — and the ground the word stands on is a tenth of the severity.
    static string PointedWash(string severity, BrandTheme theme)
    {
        return BrandTone.Mix(theme.Surface, severity, BrandTone.RowWashPointed);
    }

    // And a row with no wash at all under the same pointer — every grid in the house, since
    // NsTable hovers every row it draws. The tint is the palette's own RowHover, alpha and all:
    // composed here rather than copied as a number, so a scheme that re-tunes how hard a row
    // answers the pointer re-grades every status word with it. StatusInkTests is where a browser
    // resolves the whole cascade on the real screen.
    static string PointedRow(BrandTheme theme)
    {
        var tint = theme.RowHover.AsSpan().TrimStart('#');

        // A hover tint is only composable while its alpha is written: an opaque #rrggbb there is
        // a scheme answering the pointer with a colour of its own, which this would read as the
        // row's ground at full strength and grade every word against the wrong floor.
        Assert.True(tint.Length == 8, $"RowHover {theme.RowHover} carries no alpha pair");

        var alpha = int.Parse(tint[6..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;

        return BrandTone.Mix(theme.Surface, $"#{tint[..6]}", alpha);
    }

    // The scheme as the vendor receives it, which is where an unset slot becomes a colour
    // nobody chose: BrandTheme alone cannot say what is on the screen.
    static Palette Palette(BrandTheme theme)
    {
        var mud = BrandMudTheme.From(new Brand { Dark = theme, Light = theme });

        return theme.IsDark ? mud.PaletteDark : mud.PaletteLight;
    }

    static void Clears(string ink, string ground, double floor, string scheme)
    {
        Assert.True(Ratio(ink, ground) >= floor, $"{scheme}: {Reading(ink, ink, ground)} < {floor}");
    }

    static string Reading(string name, string ink, string ground)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{name} on {ground} is {Ratio(ink, ground):0.00}:1");
    }

    // WCAG 2.2's contrast ratio, on its own relative luminance — the channels are linearized
    // first, which is what tells this apart from BrandTone.Luminance, the ranking one.
    // Deliberately a second reading of the criterion: BrandTone computes one of its own to
    // DECIDE the tonal ink, and a test that borrowed it could only agree with itself.
    static double Ratio(string first, string second)
    {
        var high = Math.Max(Luminance(first), Luminance(second));
        var low = Math.Min(Luminance(first), Luminance(second));

        return (high + 0.05) / (low + 0.05);
    }

    static double Luminance(string color)
    {
        var digits = color.AsSpan().TrimStart('#');

        var red = Channel(digits[..2]);
        var green = Channel(digits[2..4]);
        var blue = Channel(digits[4..6]);

        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    // OKLCH chroma, read off the rung rather than borrowed from BrandTone.Tint's own OKLab
    // conversion: the derivation sets the chroma, so it cannot be the one thing that grades it.
    // Björn Ottosson's OKLab, from linear sRGB.
    static double Chroma(string color)
    {
        var digits = color.AsSpan().TrimStart('#');

        var red = Channel(digits[..2]);
        var green = Channel(digits[2..4]);
        var blue = Channel(digits[4..6]);

        var l = Math.Cbrt((0.4122214708 * red) + (0.5363325363 * green) + (0.0514459929 * blue));
        var m = Math.Cbrt((0.2119034982 * red) + (0.6806995451 * green) + (0.1073969566 * blue));
        var s = Math.Cbrt((0.0883024619 * red) + (0.2817188376 * green) + (0.6299787005 * blue));

        var a = (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s);
        var b = (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s);

        return Math.Sqrt((a * a) + (b * b));
    }

    static double Channel(ReadOnlySpan<char> pair)
    {
        var value = int.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;

        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
