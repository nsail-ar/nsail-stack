// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Tones;

namespace NSail.Types.Tests;

/// <summary>"Se deriva un tono, no el color crudo" (Leonardo through Cap, 2026-08-27). Since
/// nsail#731 the chrome and the accent are both picked raw, so what is derived is only the ink
/// they are read in: black, white and a neon are picks a colour field cannot stop anybody
/// making, and each of them still has to leave text readable.</summary>
public class BrandToneTests
{
    // BrandTheme's light scheme, written out rather than referenced: NSail.Types sits under the
    // component layer and cannot see it. The pair is only a stand-in for "a light surface and
    // the ink that reads on it" — BrandThemeContrastTests is where the real constants are swept.
    const string LightSurface = "#fbfaf8";
    const string LightInk = "#262624";

    // The lean the rung shipped with, and the top rung AccentInk starts from.
    const double FullLean = 0.55;

    // nsail#731: the chrome is picked freely and the ink that reads on it is derived from its
    // own luminance — the whole reason a free colour is never an illegible one. WCAG AA for the
    // primary ink, the 3.0 floor for the quieter one, over every pick a colour field allows.
    [Theory]
    [InlineData("#262420")]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    [InlineData("#fbfaf8")]
    [InlineData("#101418")]
    [InlineData("#2a3f84")]
    [InlineData("#f6ff00")]
    public void TheInkClearsTheChromeItSitsOn(string chrome)
    {
        var ink = BrandTone.Ink(chrome);

        Assert.True(Contrast(chrome, ink.Primary) >= 4.5, $"{ink.Primary} on {chrome}");
        Assert.True(Contrast(chrome, ink.Secondary) >= 3.0, $"{ink.Secondary} on {chrome}");
    }

    // The inversion is the chrome's alone and owes nothing to the scheme around it: a light
    // chrome reads dark ink even when the app is running its dark palette.
    [Theory]
    [InlineData("#262420", true)]
    [InlineData("#101418", true)]
    [InlineData("#2a3f84", true)]
    [InlineData("#fbfaf8", false)]
    [InlineData("#ffffff", false)]
    [InlineData("#f6ff00", false)]
    public void TheInkInvertsOnTheChromesOwnLuminance(string chrome, bool lightInk)
    {
        var ink = BrandTone.Ink(chrome);

        Assert.Equal(lightInk, Luminance(ink.Primary) > Luminance(chrome));
        Assert.Equal(lightInk, Luminance(ink.Secondary) < Luminance(ink.Primary));
    }

    // Only luminosity moves: a brand picked in a warm near-black reads a warm ink, not a
    // neutral grey sitting on a warm band.
    [Theory]
    [InlineData("#262420")]
    [InlineData("#2a3f84")]
    public void TheInkMovesLuminosityAndNotHue(string chrome)
    {
        var ink = BrandTone.Ink(chrome);

        Assert.Equal(Order(chrome), Order(ink.Secondary));
    }

    // Unrankable in, the plain pair out: mixing from a colour that did not parse would hand
    // the chrome itself back as its own text, which is a band with no writing on it.
    [Theory]
    [InlineData("")]
    [InlineData("rebeccapurple")]
    public void AnUnreadableChromeFallsBackToPlainInk(string chrome)
    {
        var ink = BrandTone.Ink(chrome);

        Assert.Equal("#ffffff", ink.Primary);
        Assert.NotEqual(chrome, ink.Secondary);
    }

    // The states are a tone of the accent, not four unrelated colours: hover lifts, pressed
    // sinks, and neither becomes the accent itself.
    [Theory]
    [InlineData("#F68E1E")]
    [InlineData("#2a3f84")]
    [InlineData("#f6ff00")]
    public void TheStatesLiftAndSinkAroundTheAccent(string accent)
    {
        var accents = BrandTone.Accents(accent);

        Assert.True(Luminance(accents.Pressed) < Luminance(accent));
        Assert.True(Luminance(accent) < Luminance(accents.Hover));
        Assert.True(Luminance(accents.Hover) < Luminance(accents.Focus));
        Assert.NotEqual(accent, accents.Pressed, StringComparer.OrdinalIgnoreCase);
    }

    // The case Óptica Lúmina's yellow made unavoidable: white on a bright accent is
    // unreadable, so the text over it turns dark instead.
    [Theory]
    [InlineData("#f6ff00", "#1a1a1a")]
    [InlineData("#ffffff", "#1a1a1a")]
    [InlineData("#2a3f84", "#ffffff")]
    [InlineData("#000000", "#ffffff")]
    public void TextOnAccentClearsTheAccentItSitsOn(string accent, string expected)
    {
        var accents = BrandTone.Accents(accent);

        Assert.Equal(expected, accents.TextOn);
        Assert.True(
            Contrast(accent, accents.TextOn) >= 4.5,
            $"{accents.TextOn} on {accent}");
    }

    // The third entry, and the one that answers to two colours instead of one: the accent worn as
    // INK leans from the scheme's own ink toward the accent and stops where the reading stops
    // clearing. Over the light scheme the house orange keeps the full lean on a bare surface and
    // a pastel cannot — which is the whole finding: a fixed blend reads one and not the other.
    [Theory]
    [InlineData("#F68E1E", true)]
    [InlineData("#2a3f84", true)]
    [InlineData("#f7c9d5", false)]
    [InlineData("#ffffff", false)]
    [InlineData("#f6ff00", false)]
    public void TheAccentInkKeepsTheFullLeanOnlyWhileItReads(string accent, bool full)
    {
        var ink = BrandTone.AccentInk(accent, LightInk, LightSurface);

        Assert.Equal(full, ink == BrandTone.Mix(LightInk, accent, FullLean));
        Assert.True(Contrast(LightSurface, ink) >= 4.5, $"{ink} on {LightSurface}");
    }

    // Every ground at once, which is what the grounds the house paints this ink on come to: a
    // wash of the accent over the surface is the other one, and the lean that clears a bare
    // surface does not always clear it. The answer reads on BOTH or the entry has not done its
    // job — nsail#1940, where one value had to serve a card, a tab strip and a washed row.
    [Theory]
    [InlineData("#F68E1E")]
    [InlineData("#2a3f84")]
    [InlineData("#f7c9d5")]
    [InlineData("#ffffff")]
    [InlineData("#000000")]
    [InlineData("#f6ff00")]
    public void TheAccentInkClearsEveryGroundItIsGiven(string accent)
    {
        var wash = BrandTone.Mix(LightSurface, accent, BrandTone.WashWeight);
        var ink = BrandTone.AccentInk(accent, LightInk, LightSurface, wash);

        Assert.True(Contrast(LightSurface, ink) >= 4.5, $"{ink} on {LightSurface}");
        Assert.True(Contrast(wash, ink) >= 4.5, $"{ink} on the wash of {accent}");
    }

    // A ground the ink cannot clear at any lean leaves it leaning nowhere rather than leaning
    // as far as it likes: the scheme's plain ink is what the caller already knew reads.
    [Fact]
    public void AGroundNoLeanClearsLeavesTheInkPlain()
    {
        Assert.Equal(LightInk, BrandTone.AccentInk("#F68E1E", LightInk, LightSurface, "#8e5a21"));
    }

    // Unrankable in, the scheme's own ink out: an accent nobody can measure is one nothing can
    // safely lean toward, and the plain ink is a colour the surface already clears.
    [Theory]
    [InlineData("")]
    [InlineData("rebeccapurple")]
    public void AnUnreadableAccentLeavesTheInkPlain(string accent)
    {
        Assert.Equal(LightInk, BrandTone.AccentInk(accent, LightInk, LightSurface));
    }

    // The derivation is a function of its seed and nothing else: saving the same brand twice
    // has to land on the same colours, or every save would walk them one step further.
    [Fact]
    public void DerivingTwiceFromTheSameSeedLandsOnTheSameColours()
    {
        Assert.Equal(BrandTone.Ink("#2a8400"), BrandTone.Ink("#2a8400"));
        Assert.Equal(BrandTone.Accents("#2a8400"), BrandTone.Accents("#2a8400"));
        Assert.Equal(
            BrandTone.AccentInk("#2a8400", LightInk, LightSurface),
            BrandTone.AccentInk("#2a8400", LightInk, LightSurface));
    }

    // #rgb and #rrggbbaa are both shapes a colour field can hand over; alpha is read past
    // because surfaces are opaque.
    [Theory]
    [InlineData("#fff", "#ffffff")]
    [InlineData("#ffffff00", "#ffffff")]
    public void ShorthandAndAlphaReadAsTheSameColour(string seed, string equivalent)
    {
        Assert.Equal(BrandTone.Ink(equivalent).Primary, BrandTone.Ink(seed).Primary);
        Assert.Equal(BrandTone.Accents(equivalent).TextOn, BrandTone.Accents(seed).TextOn);
    }

    // The order of the channels, which is what "same hue" comes to when only luminosity moves.
    static string Order(string color)
    {
        var channels = Channels(color);

        return string.Join(
            ",",
            channels
                .Select((value, index) => (value, index))
                .OrderBy(channel => channel.value)
                .Select(channel => channel.index));
    }

    // WCAG 2.1's own contrast ratio: 4.5 is the AA floor for body text.
    static double Contrast(string background, string foreground)
    {
        var one = Relative(background);
        var other = Relative(foreground);

        return (Math.Max(one, other) + 0.05) / (Math.Min(one, other) + 0.05);
    }

    static double Relative(string color)
    {
        var channels = Channels(color).Select(Linear).ToArray();

        return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
    }

    static double Luminance(string color)
    {
        var channels = Channels(color);

        return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
    }

    static double Linear(double value)
    {
        var part = value / 255.0;

        return part <= 0.03928 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
    }

    static double[] Channels(string color)
    {
        var digits = color.TrimStart('#');

        return
        [
            Convert.ToInt32(digits[..2], 16),
            Convert.ToInt32(digits[2..4], 16),
            Convert.ToInt32(digits[4..6], 16),
        ];
    }
}
