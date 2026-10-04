// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text.Json.Serialization;
using NSail.Tones;

namespace NSail.Components;

/// <summary>One color scheme of a <see cref="Brand"/> (light or dark), and THE palette: every
/// surface, line, ink and status colour below is a constant of the scheme, so a stored brand
/// carries only what a tenant actually picked (its accent, its chrome and its logos) and
/// overlays it on top of <see cref="DefaultDark"/>/<see cref="DefaultLight"/>. Hex strings,
/// serializable.</summary>
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

    /// <summary>The label on the accent's soft wash (NsAs.Important), which stands on that wash
    /// of <see cref="Accent"/> over <see cref="Surface"/> rather than on the accent itself: the
    /// scheme's own ink leaned toward the accent by as much as clears AA over that wash.
    /// Derived at paint from a pick and two scheme constants, so it is stored nowhere and
    /// travels nowhere.</summary>
    // Off the wire too, and not only out of the column: the prerender hands this whole object
    // to the client, which derives the same answer from the same three values. A copy on that
    // payload is a second one that a client on an older build could disagree with.
    [JsonIgnore]
    public string TextOnAccentSoft => BrandTone.SoftInk(Accent, Surface, TextPrimary);

    // Surfaces and text

    /// <summary>The colour the app bar and the drawer wear as one band — the second colour a
    /// brand is picked in, free of the surfaces below it.</summary>
    public string Chrome { get; set; } = "#201f1b";

    /// <summary>Text and icons drawn on top of <see cref="Chrome"/>: dark on a light chrome,
    /// light on a deep one, in either scheme. Never the content's own text colour.</summary>
    public string TextOnChrome { get; set; } = "#dedddd";

    /// <summary>The quieter ink on <see cref="Chrome"/>: drawer icons and secondary marks.</summary>
    public string TextOnChromeSecondary { get; set; } = "#aaaaa8";

    /// <summary>The canvas everything sits on. Fixed per scheme, never derived from a pick.</summary>
    // In dark, elevation is LIGHT and not shadow — a black shadow on a black surface is
    // invisible — so the ladder has to be spent on tone: the canvas sits far enough below the
    // surface for a card to read as lifted without a border. Pure black is nobody's floor
    // either (Material's is #121212): white type on absolute black hazes.
    public string Background { get; set; } = "#161513";

    /// <summary>Content surfaces: panels, cards, tables. Fixed per scheme, so a dark theme can
    /// never carry a white background.</summary>
    public string Surface { get; set; } = "#232120";

    /// <summary>Surfaces that float over another one: dialogs, menus, autocomplete lists.</summary>
    public string SurfaceRaised { get; set; } = "#2d2b27";

    public string TextPrimary { get; set; } = "#e7e4dd";

    public string TextSecondary { get; set; } = "#9c9992";

    /// <summary>The ink of a control that cannot be used yet — a disabled button's word, a
    /// disabled glyph, a greyed menu row. The ladder's third rung, a clear step under
    /// <see cref="TextSecondary"/>: a refused act has to read as refused at a glance and still
    /// be READABLE, which is what puts it around 2.5:1 on its own fill rather than at the
    /// muted rank's 5.6:1.</summary>
    // A scheme constant and never derived from the accent, unlike the accent's own states above:
    // a refused control is the one thing the brand may not claim, and an ink carrying the
    // tenant's colour puts a disabled button and an NsAs.Important one under two warm labels on
    // two muted fills — one picture.
    public string TextDisabled { get; set; } = "#6b6864";

    /// <summary>Borders, dividers and table lines.</summary>
    public string Lines { get; set; } = "#312f2b";

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

    public static BrandTheme DefaultDark()
    {
        return new();
    }

    // Light separates surfaces with borders and shadows rather than tone — white on
    // white has nowhere to go — so the canvas sinks below the surfaces instead.
    public static BrandTheme DefaultLight()
    {
        return new()
        {
            Chrome = "#fcfbf9",
            TextOnChrome = "#262625",
            TextOnChromeSecondary = "#6a6969",
            Background = "#f1f0ee",
            Surface = "#fbfaf8",
            SurfaceRaised = "#fcfbf9",
            TextPrimary = "#262624",
            TextSecondary = "#6e6b66",
            TextDisabled = "#9a9792",
            Lines = "#e4e2de",
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
