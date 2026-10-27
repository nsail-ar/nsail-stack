// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json.Serialization;
using NSail.Tones;

namespace NSail.Components;

/// <summary>One color scheme of a <see cref="Brand"/> (light or dark), and THE palette: every
/// status colour below is a constant of the scheme, and the surface and ink ladder is the
/// scheme's own luminance worn in the chrome's hue, so a stored brand carries only what a tenant
/// actually picked (its accent, its chrome and its logos) and overlays it on top of
/// <see cref="DefaultDark"/>/<see cref="DefaultLight"/>. Hex strings, serializable.</summary>
public sealed class BrandTheme
{
    public string? Logo { get; set; }

    // Accent

    /// <summary>The one colour a brand is picked in. Everything else on the accent side is a
    /// state of it, derived by whoever stores the brand.</summary>
    public string Accent { get; set; } = "#F68E1E";

    public string AccentHover { get; set; } = "#f79c39";

    public string AccentPressed { get; set; } = "#d47a1a";

    public string AccentFocus { get; set; } = "#f9b062";

    /// <summary>Text and icons drawn on top of <see cref="Accent"/>: dark on a bright accent,
    /// light on a deep one, so a yellow brand stays readable.</summary>
    public string TextOnAccent { get; set; } = "#1a1a1a";

    /// <summary>The accent AS INK on the content's own surfaces — NsAs.Important's label, a
    /// link, the active tab, the guide chapter the reader is on. The scheme's own ink leaned
    /// toward <see cref="Accent"/> by as much as clears AA on every one of those grounds: the
    /// accent itself is a ground and never text, which is what reads 2.3:1 on a pale surface.
    /// Derived at paint from a pick and the scheme's constants, so it is stored nowhere and
    /// travels nowhere.</summary>
    // Off the wire too, and not only out of the column: the prerender hands this whole object
    // to the client, which derives the same answer from the same values. A copy on that payload
    // is a second one that a client on an older build could disagree with.
    [JsonIgnore]
    public string AccentInk => BrandTone.AccentInk(Accent, TextPrimary, InkGrounds());

    /// <summary>The accent as ink ON the chrome — the drawer entry the reader is on, and the
    /// guide's open chapter in that same gutter. The band is picked freely while the luminance
    /// of the content's surfaces is fixed, so its accent ink is leaned from
    /// <see cref="TextOnChrome"/> and read against the chrome, never borrowed from <see cref="AccentInk"/>: a light chrome can sit
    /// under a dark scheme.</summary>
    [JsonIgnore]
    public string AccentOnChrome => BrandTone.AccentInk(
        Accent,
        TextOnChrome,
        Chrome,
        BrandTone.Mix(Chrome, TextOnChrome, BrandTone.RailWeight));

    // Surfaces and text

    /// <summary>The colour the app bar and the drawer wear as one band — the second colour a
    /// brand is picked in. The surfaces and inks below it wear its hue, never its luminance.</summary>
    public string Chrome { get; set; } = "#201f1b";

    /// <summary>Text and icons drawn on top of <see cref="Chrome"/>: dark on a light chrome,
    /// light on a deep one, in either scheme. Never the content's own text colour.</summary>
    public string TextOnChrome { get; set; } = "#dedddd";

    /// <summary>The quieter ink on <see cref="Chrome"/>: drawer icons and secondary marks.</summary>
    public string TextOnChromeSecondary { get; set; } = "#aaaaa8";

    /// <summary>Which ladder the scheme stands on. The only thing about the ladder that travels:
    /// its rungs are derived from it and <see cref="Chrome"/>, at paint, wherever this object is.</summary>
    public bool IsDark { get; set; } = true;

    /// <summary>The canvas everything sits on: the scheme's luminance, in the chrome's hue.</summary>
    [JsonIgnore]
    public string Background => Rung(Ladder.Background, BrandTone.SurfaceTint);

    /// <summary>Content surfaces: panels, cards, tables. Their luminance is the scheme's, so a
    /// dark theme can never carry a white background whatever the chrome.</summary>
    [JsonIgnore]
    public string Surface => Rung(Ladder.Surface, BrandTone.SurfaceTint);

    /// <summary>Surfaces that float over another one: dialogs, menus, autocomplete lists.</summary>
    [JsonIgnore]
    public string SurfaceRaised => Rung(Ladder.SurfaceRaised, BrandTone.SurfaceTint);

    [JsonIgnore]
    public string TextPrimary => Rung(Ladder.TextPrimary, BrandTone.InkTint);

    [JsonIgnore]
    public string TextSecondary => Rung(Ladder.TextSecondary, BrandTone.InkTint);

    /// <summary>The ink of a control that cannot be used yet — a disabled button's word, a
    /// disabled glyph, a greyed menu row. The ladder's third rung, a clear step under
    /// <see cref="TextSecondary"/>: a refused act has to read as refused at a glance and still
    /// be READABLE, which is what puts it around 2.5:1 on its own fill rather than at the
    /// muted rank's 5.6:1.</summary>
    // A scheme constant and never derived from the accent, unlike the accent's own states above:
    // a refused control is the one thing the brand may not claim, and an ink carrying the
    // tenant's colour puts a disabled button and an NsAs.Important one under two warm labels on
    // two muted fills — one picture.
    [JsonIgnore]
    public string TextDisabled => Rung(Ladder.TextDisabled, BrandTone.InkTint);

    /// <summary>Borders, dividers and table lines — decorative, so the rung sits well under
    /// <see cref="LinesInputs"/>'s floor: a row or an outlined paper is told apart by its own
    /// generous padding too, an outlined FIELD is not.</summary>
    [JsonIgnore]
    public string Lines => Rung(Ladder.Lines, BrandTone.SurfaceTint);

    /// <summary>The border of an outlined field or select at rest — a boundary nobody is
    /// reading text off, so it owes the large floor (3:1) rather than the body one, and reads
    /// as a glyph rather than a fill (ui/branding.md's accent-as-ink reasoning applies the same
    /// way to a free-standing line). Its own rung because <see cref="Lines"/> stays decorative:
    /// a table row or an outlined paper is told apart by padding too, an empty field is not, so
    /// darkening <see cref="Lines"/> itself to this floor would turn every hairline in the house
    /// into a ruled page. Dark needs no rung of its own — a light line on a dark surface already
    /// clears it — so the mask below repeats <see cref="Lines"/>' own dark value.</summary>
    [JsonIgnore]
    public string LinesInputs => Rung(Ladder.LinesInputs, BrandTone.SurfaceTint);

    /// <summary>Row hover — the only tint a row wears, and the second thing after
    /// <see cref="Lines"/> that tells two rows apart. Translucent, so it tints whatever surface
    /// it lands on.</summary>
    public string RowHover { get; set; } = "#ffffff14";

    // Status
    public string Error { get; set; } = "#ef5350";

    public string Warning { get; set; } = "#f0a441";

    public string Success { get; set; } = "#66bb6a";

    public string Info { get; set; } = "#64b5f6";

    /// <summary>Text and icons drawn ON a severity's own colour at full strength: a filled
    /// banner, a toast, a filled chip or avatar. Each is read against its own fill and not
    /// against a surface, so a scheme carries one per severity.</summary>
    // Near-black or white, whichever clears AA on that fill — no single ink serves all eight
    // faces, and no luminance ranking picks them either: #ef5350 ranks dark, which would put
    // white on it at 3.49:1. The dark half of the pair is #121212 and not the accent's #1a1a1a
    // because the light scheme's Info leaves #1a1a1a at 4.51:1, a hundredth of margin.
    public string TextOnError { get; set; } = "#121212";

    public string TextOnWarning { get; set; } = "#121212";

    public string TextOnSuccess { get; set; } = "#121212";

    public string TextOnInfo { get; set; } = "#121212";

    // Every ground AccentInk lands on: the three rungs of the ladder, and the accent's own soft
    // wash over each of them — --ns-accent-soft mixes against transparent, so NsAs.Important's
    // label stands on the wash over whichever rung the button happens to sit on. The vendor's
    // 6% hover (the ground under the active tab and the open chapter) needs no entry of its own:
    // it lies between a bare rung and the tenth below it, and the ink is never between the two,
    // so the pair bounds it.
    // A ROW UNDER THE POINTER is not among them, unlike the status channel's grounds
    // (BrandThemeContrastTests, nsail#1935): nothing in the house sets a word in this ink inside a
    // grid row — a RowEditor cell's field asks for no visible label at all (ui/hosts.md), so there
    // is no floating label to promote there. Adding it anyway would cost the default brand two
    // more steps of its own hue on every tab in the app, to answer a screen that does not exist.
    string[] InkGrounds()
    {
        return
        [
            Background,
            Surface,
            SurfaceRaised,
            BrandTone.Mix(Background, Accent, BrandTone.WashWeight),
            BrandTone.Mix(Surface, Accent, BrandTone.WashWeight),
            BrandTone.Mix(SurfaceRaised, Accent, BrandTone.WashWeight),
        ];
    }

    // The ladder as a MASK: each rung's relative luminance is the scheme's and fixed — the steps
    // the contrast floors are measured on — while its hue and chroma are the chrome's, capped
    // (BrandTone.Tint). Written as the hexes the default brand wore, so the numbers stay
    // readable; only their luminance survives the tint.
    // Main and aside stand on Background and Surface respectively (ui/branding.md's
    // inherit-the-ground rule), so the step between the two rungs is what tells them apart: close
    // enough that a component reads the same on either, far enough to clear at a glance, with
    // elevation (the ring in dark, the shadow in light) carrying the rest. Pure black is
    // nobody's floor either (Material's is #121212): white type on absolute black hazes.
    static readonly LadderMask DarkLadder = new(
        Background: "#161513",
        Surface: "#1c1a18",
        SurfaceRaised: "#201f1c",
        TextPrimary: "#e7e4dd",
        TextSecondary: "#9c9992",
        TextDisabled: "#65635f",
        Lines: "#312f2b",
        LinesInputs: "#312f2b");

    // Light has no room to sink the surfaces further up than white, so the step is spent on
    // Background instead: the mask stays close enough to white to keep backing its own chroma off
    // through the same narrow sRGB gamut (BrandTone.Tint) that a mask with more gamut to spare
    // would ride to a coloured page under a vivid chrome — Surface and SurfaceRaised sit AT white,
    // which the ceiling below holds there.
    static readonly LadderMask LightLadder = new(
        Background: "#faf9f8",
        Surface: "#ffffff",
        SurfaceRaised: "#ffffff",
        TextPrimary: "#262624",
        TextSecondary: "#6e6b66",
        TextDisabled: "#9a9792",
        Lines: "#d0cdc8",
        LinesInputs: "#757575");

    LadderMask Ladder => IsDark ? DarkLadder : LightLadder;

    string Rung(string mask, double cap)
    {
        return BrandTone.Tint(mask, Chrome, cap);
    }

    sealed record LadderMask(
        string Background,
        string Surface,
        string SurfaceRaised,
        string TextPrimary,
        string TextSecondary,
        string TextDisabled,
        string Lines,
        string LinesInputs);

    public static BrandTheme DefaultDark()
    {
        return new();
    }

    public static BrandTheme DefaultLight()
    {
        return new()
        {
            IsDark = false,
            Chrome = "#fcfbf9",
            TextOnChrome = "#262625",
            TextOnChromeSecondary = "#6a6969",
            RowHover = "#00000014",
            Error = "#d32f2f",
            Warning = "#c77700",
            Success = "#2e7d32",
            Info = "#0288d1",
            TextOnError = "#ffffff",
            TextOnSuccess = "#ffffff",
        };
    }
}
