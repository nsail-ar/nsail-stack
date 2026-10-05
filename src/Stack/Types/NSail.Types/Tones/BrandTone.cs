// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Tones;

// The one tonal helper the brand has. Three entries and nothing else: the ink a Chrome is read
// in, the states an Accent answers with, and the Accent AS INK — the label, the link, the tab
// that says "you are here". A second implementation of any of them is what let the chrome and
// the surfaces drift apart.
public static class BrandTone
{
    /// <summary>The accent's share of the soft wash NsAs.Important is filled with — the
    /// stylesheet's <c>--ns-accent-soft</c>, which mixes against transparent so one value reads
    /// on a card, a raised sheet and the canvas alike. It is named here because whoever measures
    /// <see cref="AccentInk"/> has to compose the same wash to read it against.</summary>
    public const double WashWeight = 0.13;

    /// <summary>The chrome ink's share of the wash the drawer marks a row with — the
    /// stylesheet's <c>--ns-rail-hover</c>, the ground under the entry the reader is on. Named
    /// here beside its siblings for the reason <see cref="WashWeight"/> is: the accent's ink on
    /// that band has to be read against the ground it actually stands on.</summary>
    public const double RailWeight = 0.08;

    /// <summary>The scheme ink's share of the fill a REFUSED control is drawn on — the
    /// stylesheet's <c>--ns-inert-soft</c>, mixed against transparent for the same reason
    /// <see cref="WashWeight"/>'s is. Named here beside its sibling because a ground has to be
    /// composable by whatever measures the label standing on it, and deliberately half the
    /// vendor's own 12%, at which the one act nobody can press is the loudest grey on the
    /// screen.</summary>
    public const double InertWeight = 0.06;

    /// <summary>The severity's share of the ink a STATE's word is painted in — the stylesheet's
    /// <c>--ns-status-*</c>, mixed into the scheme's own TextPrimary so one fraction leans dark
    /// in the light scheme and light in the dark one. Named here because
    /// a severity constant is a FILL and reading it as 16px of text is a criterion it does not
    /// meet on its own; whatever measures the word has to compose the same mix.</summary>
    public const double ToneLean = 0.65;

    /// <summary>The muted ink's share of the NEUTRAL state's word — the stylesheet's
    /// <c>.ns-status-muted</c>, leaned the rest of the way into TextPrimary. The muted rank on
    /// its own is tuned against a surface, and a status word is read in a table row, which tints
    /// under the pointer; the tenth is what carries it over the body floor there while keeping
    /// the word a visible step under the ink of the cells beside it.</summary>
    public const double NeutralLean = 0.90;

    /// <summary>The severity's share of the tint a washed grid ROW carries — the stylesheet's
    /// <c>.ns-row-*</c>. Ours rather than the vendor's <c>--mud-palette-{severity}-hover</c>
    /// alias it used to borrow: the floor of every status word standing on that row is a
    /// function of this number, so a vendor bump may not move it in silence.</summary>
    public const double RowWash = 0.06;

    /// <summary>The same tint under the POINTER. The vendor answers a hover by REPLACING a row's
    /// background with its own neutral tint, which on a washed row blanks the condition channel
    /// on the one row being read; the stylesheet hands the vendor's own
    /// <c>--mud-palette-table-hover</c> variable this value per washed row instead, so the wash
    /// deepens in its own tone rather than being painted over.</summary>
    public const double RowWashPointed = 0.10;

    // The ink, as a distance from the chrome: towards white over a dark chrome, towards black
    // over a light one. Mixed from the chrome and not picked, so the chrome's own warmth
    // carries into its text instead of a neutral grey sitting on a warm band.
    const double InkLift = 0.85;
    const double SecondaryInkLift = 0.62;
    const double SecondaryInkDrop = 0.58;

    // How far that label may lean toward the accent, walked down until the reading clears.
    // Counted in twentieths rather than stepped by 0.05, so every rung is the exact double a
    // literal would be and the same accent can never land on two neighbouring hexes.
    const int FullLean = 11;
    const int LeanSteps = 20;

    // WCAG 2.2 AA for body text, which a button's label is.
    const double BodyContrast = 4.5;

    const double HoverLift = 0.12;
    const double PressedDrop = 0.14;
    const double FocusLift = 0.30;

    const string Black = "#000000";
    const string White = "#ffffff";
    const string Grey = "#808080";

    // Anything brighter than this reads as a light colour, so the ladder inverts and text on
    // the accent turns dark. Perceived luminance, not the raw average: a saturated yellow and
    // a saturated blue of the same average are nowhere near as bright to the eye.
    const double LightThreshold = 0.55;

    // The chrome is picked freely and paints the appbar and the drawer alone, so what makes it
    // readable is its own luminance and nothing else: a light chrome reads dark ink in either
    // scheme, a dark one reads light ink. The content's own text never crosses onto the band.
    // A colour nobody can rank falls back to plain white and grey rather than to a mix of a
    // value that did not parse, which Mix would hand back as the chrome itself.
    public static BrandInk Ink(string chrome)
    {
        if (Luminance(chrome) is not { } luminance)
        {
            return new BrandInk(White, Grey);
        }

        if (luminance >= LightThreshold)
        {
            return new BrandInk(Mix(chrome, Black, InkLift), Mix(chrome, Black, SecondaryInkDrop));
        }

        return new BrandInk(Mix(chrome, White, InkLift), Mix(chrome, White, SecondaryInkLift));
    }

    // The states the accent answers are the states of an act that can still be pressed. A
    // REFUSED one is deliberately not among them: disabled is a state of a control, every
    // control in the app wears it, and only a handful of them are ever painted in the accent —
    // a tone leaned toward a tenant's colour would be the whole app's inert ink lending the
    // brand to the one thing the brand must not claim. That ink is a scheme constant
    // (BrandTheme.TextDisabled), the same rank TextPrimary and TextSecondary sit on.
    public static BrandAccents Accents(string accent)
    {
        var luminance = Luminance(accent);

        return new BrandAccents(
            Mix(accent, White, HoverLift),
            Mix(accent, Black, PressedDrop),
            Mix(accent, White, FocusLift),
            luminance >= LightThreshold ? "#1a1a1a" : White);
    }

    // The accent AS INK: the soft rung's label, a link, the active tab, the chapter the reader
    // is on. The accent itself is made to be a GROUND — a fill, a glyph, a slider — and a free
    // pick painted as text on a pale surface is what reads 2.3:1, so the ink starts from the
    // scheme's own and is leaned toward the accent for identity, as far as AA still holds on
    // EVERY ground the house paints it on and no further. A FIXED lean cannot honour "a free
    // colour is never an illegible one": 55% of the house orange reads 4.53:1 on its own wash in
    // light, and 55% of a pastel pick reads 3.3:1 on its own. Leaning nowhere is the floor, and
    // it is the scheme's plain ink on a surface it already clears — which is also what an
    // unparseable accent falls back to, since Mix hands its own seed back rather than a colour
    // it invented.
    // Every ground at once and not the worst one: which ground is worst is itself a function of
    // the pick (a wash of a bright accent darkens a light surface and lightens a dark one), so a
    // caller that ranked them would be deciding the same question this answers.
    public static string AccentInk(string accent, string ink, params string[] grounds)
    {
        for (var lean = FullLean; lean > 0; lean--)
        {
            var candidate = Mix(ink, accent, (double)lean / LeanSteps);

            if (grounds.All(ground => Contrast(ground, candidate) >= BodyContrast))
            {
                return candidate;
            }
        }

        return ink;
    }

    public static string Mix(string from, string to, double weight)
    {
        var start = Parse(from);
        var end = Parse(to);

        if (start is null || end is null)
        {
            return from;
        }

        var red = Blend(start[0], end[0], weight);
        var green = Blend(start[1], end[1], weight);
        var blue = Blend(start[2], end[2], weight);

        return string.Create(CultureInfo.InvariantCulture, $"#{red:x2}{green:x2}{blue:x2}");
    }

    // Null when the colour does not parse, so a caller can leave an unreadable value alone
    // instead of ranking it against a number it invented.
    public static double? Luminance(string color)
    {
        if (Parse(color) is not { } channels)
        {
            return null;
        }

        return ((0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2])) / 255d;
    }

    // WCAG 2.2's own ratio, on the linearized luminance the criterion is written in. That is
    // what tells it apart from Luminance above, which RANKS a pick light or dark for the
    // inversions and never claimed to be this: a ratio needs the gamma taken out first.
    static double Contrast(string first, string second)
    {
        var one = Relative(first);
        var other = Relative(second);

        return (Math.Max(one, other) + 0.05) / (Math.Min(one, other) + 0.05);
    }

    static double Relative(string color)
    {
        if (Parse(color) is not { } channels)
        {
            return 0;
        }

        return (0.2126 * Linear(channels[0])) + (0.7152 * Linear(channels[1])) + (0.0722 * Linear(channels[2]));
    }

    static double Linear(byte channel)
    {
        var value = channel / 255d;

        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    static byte Blend(byte start, byte end, double weight)
    {
        return (byte)Math.Round(start + ((end - start) * weight));
    }

    // A colour that does not parse is left alone rather than thrown over: this runs inside a
    // save, and refusing to store a brand because a hex arrived in an unexpected shape would
    // cost the user their whole edit over a slot they never picked. Alpha (#rrggbbaa, #rgba)
    // is read past — the chrome is opaque.
    static byte[]? Parse(string color)
    {
        var digits = color.AsSpan().TrimStart('#');
        var wide = digits.Length is 6 or 8;

        if (!wide && digits.Length is not (3 or 4))
        {
            return null;
        }

        var channels = new byte[3];
        Span<char> pair = stackalloc char[2];

        for (var channel = 0; channel < channels.Length; channel++)
        {
            pair[0] = wide ? digits[channel * 2] : digits[channel];
            pair[1] = wide ? digits[(channel * 2) + 1] : digits[channel];

            if (!byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out channels[channel]))
            {
                return null;
            }
        }

        return channels;
    }
}
