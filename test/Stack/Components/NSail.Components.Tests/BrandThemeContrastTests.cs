// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using MudBlazor;
using MudBlazor.Utilities;
using NSail.Components;
using NSail.Tones;

namespace NSail.Components.Tests;

/// <summary>The palette is legible, and the arithmetic says so rather than an eye (nsail#842).
/// A scheme is free to move — that is the whole point of one palette in one place — so the
/// floor moves with it: body text clears 4.5:1 against the surface it is drawn on and the
/// quieted rank clears 3:1 as well, in both schemes, or the change does not land.</summary>
public sealed class BrandThemeContrastTests
{
    // WCAG 2.2's own two numbers. Which one binds a dashboard figure is not a free choice: the
    // large-text line sits at 18pt/24px for regular weight, and only drops to 14pt/18.66px for
    // bold. A card's figure is body-sized (ui/styling.md, nsail#1272) — Typo.body1's 16px at
    // weight 400 — so it clears neither line and the body floor is the one that binds; the
    // body-floor test below reads TextSecondary on every surface, which is that figure at its
    // quietest. The large floor is kept and still tested, one theory down.
    const double BodyFloor = 4.5;
    const double LargeFloor = 3;

    // The light scheme's page-vs-card step, held from ABOVE as well as from below. The readings
    // today are 1.09:1 for Surface and 1.10:1 for SurfaceRaised over Background, and the ceiling
    // sits just over the higher one: light separates by border and shadow, so any re-tune that
    // hardens the step into a tonal separation of its own has to move this number in the open
    // instead of drifting past an eye. Light only — dark's step is 1.14:1 and is MEANT to read
    // on its own, which is the floor below.
    const double LightStepCeiling = 1.11;

    public static TheoryData<string, BrandTheme> Schemes => new()
    {
        { "dark", BrandTheme.DefaultDark() },
        { "light", BrandTheme.DefaultLight() },
    };

    // Every filled face there is: four severities in each scheme, each with the fill the vendor
    // paints and the ink the palette hands it. Read off the palette, so a slot left unset shows
    // up here as the vendor's own white.
    public static TheoryData<string, string, string, string> Faces()
    {
        var faces = new TheoryData<string, string, string, string>();

        foreach (var (scheme, theme) in new[] { ("dark", BrandTheme.DefaultDark()), ("light", BrandTheme.DefaultLight()) })
        {
            var palette = Palette(scheme);

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

    // Against its OWN surface, which is the content's: a card, a panel, a dialog. The canvas
    // is what those stand on and not something text is set on, so it is not one of the grounds
    // asked about here.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void BodyTextClearsTheBodyFloorOnEverySurfaceItIsDrawnOn(string scheme, BrandTheme theme)
    {
        foreach (var surface in new[] { theme.Surface, theme.SurfaceRaised })
        {
            Clears(theme.TextPrimary, surface, BodyFloor, scheme);
            Clears(theme.TextSecondary, surface, BodyFloor, scheme);
        }
    }

    // The muted rank is where a zero-valued KPI lands, so it is the figure with the least
    // contrast on the board. The body floor above is what binds it and already covers it on
    // the surfaces; this keeps nsail#842's own criterion on the record, so a figure that ever
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
        var palette = Palette(scheme);

        foreach (var surface in new[] { theme.Surface, theme.SurfaceRaised })
        {
            Clears(palette.TextSecondary.Value, surface, BodyFloor, scheme);
        }
    }

    // And it stays the quiet one by being ACHROMATIC rather than by reading lower: on the light
    // card the secondary ink reads 5.09:1 while Warning reads 3.32:1, so a contrast ordering
    // would say the opposite of what the eye does. What makes a neutral state read as neutral
    // is that it carries no hue, which here means it is the ink ladder's muted rung and none of
    // the four severities.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void TheNeutralStatusWordIsTheMutedRankAndNeverASeverity(string scheme, BrandTheme theme)
    {
        var palette = Palette(scheme);

        Assert.Equal(new MudColor(theme.TextSecondary).Value, palette.TextSecondary.Value);

        foreach (var severity in new[] { theme.Error, theme.Warning, theme.Success, theme.Info })
        {
            Assert.NotEqual(new MudColor(severity).Value, palette.TextSecondary.Value);
        }
    }

    // The two bands that are not the content's surface, each read in its own ink: the accent a
    // filled button wears, and the chrome the appbar and the drawer wear.
    [Theory]
    [MemberData(nameof(Schemes))]
    public void TheAccentAndTheChromeAreReadInTheirOwnInk(string scheme, BrandTheme theme)
    {
        Clears(theme.TextOnAccent, theme.Accent, BodyFloor, scheme);
        Clears(theme.TextOnChrome, theme.Chrome, BodyFloor, scheme);
        Clears(theme.TextOnChromeSecondary, theme.Chrome, BodyFloor, scheme);
    }

    // The tonal rung's label, over the wash it actually stands on. The wash is translucent, so
    // it is composed against the scheme's own surface first — a card is where these buttons
    // live — and the label and its ground are then BOTH functions of the same free pick, which
    // is how a flat blend of the two used to converge on itself: 55% of a pastel over the light
    // scheme read 3.3:1 on its own wash, and 55% of black over the dark one read 2.97:1.
    [Theory]
    [MemberData(nameof(Picks))]
    public void TheSoftAccentsLabelClearsTheWashItStandsOn(string scheme, BrandTheme theme, string accent)
    {
        theme.Accent = accent;

        Clears(theme.TextOnAccentSoft, Wash(theme), BodyFloor, scheme);
    }

    // The theory above is the picks worth naming; this is the promise they stand for. A colour
    // field takes any of 16 million, so the floor is swept rather than sampled — coarsely, but
    // across the whole cube and in both schemes, because an accent that reads on the dark wash
    // is exactly the one that can vanish on the light one.
    [Fact]
    public void NoAccentAColourFieldAllowsLeavesTheLabelIllegible()
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

                        Clears(theme.TextOnAccentSoft, Wash(theme), BodyFloor, theme.Accent);
                    }
                }
            }
        }
    }

    // What the browser was looking at when the rung was signed off. The house accent clears AA
    // at the full lean, so moving the derivation out of the stylesheet and into a luminance
    // decision moved no pixel of the default brand — only of the brands that were unreadable.
    [Fact]
    public void TheHouseBrandsSoftAccentLabelIsUnmoved()
    {
        Assert.Equal("#efb574", BrandTheme.DefaultDark().TextOnAccentSoft);
        Assert.Equal("#985f21", BrandTheme.DefaultLight().TextOnAccentSoft);
    }

    // The light scheme separates by border and shadow rather than by tone, and the dark one by
    // tone: white has nowhere left to rise, black has. So the canvas sinking BELOW the surfaces
    // is what both schemes owe, and only the dark one owes a step big enough to read on its
    // own — 1.04:1 before this story was no step at all.
    [Fact]
    public void TheCanvasSinksBelowTheSurfacesInBothSchemes()
    {
        var dark = BrandTheme.DefaultDark();
        var light = BrandTheme.DefaultLight();

        Assert.True(Luminance(dark.Background) < Luminance(dark.Surface));
        Assert.True(Luminance(dark.Surface) < Luminance(dark.SurfaceRaised));
        Assert.True(Luminance(light.Background) < Luminance(light.Surface));

        Assert.True(
            Ratio(dark.Surface, dark.Background) >= 1.1,
            Reading("dark card", dark.Surface, dark.Background));
    }

    // The order above says the canvas is under the surfaces; this says how far under, in the
    // scheme where the separating is done by border and shadow. A step a reader reads as a hard
    // edge of tone is the clash this answers, and "gentle" is a number a run keeps rather than a
    // screenshot somebody once approved.
    [Fact]
    public void TheLightCanvasToCardStepStaysGentle()
    {
        var light = BrandTheme.DefaultLight();

        Assert.True(
            Ratio(light.Surface, light.Background) <= LightStepCeiling,
            $"{Reading("light card", light.Surface, light.Background)} > {LightStepCeiling}");

        Assert.True(
            Ratio(light.SurfaceRaised, light.Background) <= LightStepCeiling,
            $"{Reading("light raised card", light.SurfaceRaised, light.Background)} > {LightStepCeiling}");
    }

    // What the eye is given, not what the rule says: --ns-accent-soft is mixed against
    // transparent, so the ground is the accent's tenth composited over the surface under it.
    // The mixing is BrandTone's own (it is arithmetic, and a second lerp here would only be a
    // second way to be wrong); the reading below is this file's, so the derivation is never
    // graded by its own contrast maths.
    static string Wash(BrandTheme theme)
    {
        return BrandTone.Mix(theme.Surface, theme.Accent, BrandTone.WashWeight);
    }

    // The scheme as the vendor receives it, which is where an unset slot becomes a colour
    // nobody chose: BrandTheme alone cannot say what is on the screen.
    static Palette Palette(string scheme)
    {
        var theme = BrandMudTheme.From(new Brand());

        return scheme == "dark" ? theme.PaletteDark : theme.PaletteLight;
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

    static double Channel(ReadOnlySpan<char> pair)
    {
        var value = int.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;

        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
