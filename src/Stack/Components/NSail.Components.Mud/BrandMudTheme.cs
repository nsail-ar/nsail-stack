// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using MudBlazor;
using MudBlazor.Utilities;

namespace NSail.Components;

/// <summary>Translates the vendor-agnostic <see cref="Brand"/> into a MudBlazor theme.</summary>
public static class BrandMudTheme
{
    public static MudTheme From(Brand brand)
    {
        return new MudTheme
        {
            PaletteLight = Map(brand.Light, new PaletteLight()),
            PaletteDark = Map(brand.Dark, new PaletteDark()),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "6px",
            },
            Shadows = new Shadow
            {
                Elevation = BuildElevations(),
            },
            // MudBlazor's own overlays never joined SurfaceContext's escalation chain: its
            // defaults (Dialog 1400, Popover 1200, ...) were picked with no idea an aside
            // sits at 1400 or a modal at 1500, so a vendor Confirm opened from either could
            // paint behind it, backdrop and all. SurfaceContext.VendorZIndex is the one tier
            // beyond the whole chain (the Dialog surface's own tier) — every vendor overlay
            // reads it here instead of MudBlazor's defaults, so a Confirm, an Open<T> host or
            // a select's popover always outranks whatever surface it came from.
            ZIndex = new MudBlazor.ZIndex
            {
                Dialog = SurfaceContext.VendorZIndex,
                Popover = SurfaceContext.VendorZIndex,
                Snackbar = SurfaceContext.VendorZIndex,
                Tooltip = SurfaceContext.VendorZIndex,
            },
        };
    }

    static TPalette Map<TPalette>(BrandTheme theme, TPalette palette)
        where TPalette : Palette
    {
        // The vendor would derive its own lighten/darken/contrast off Primary; the brand's
        // states are already derived and stored, so they are handed over instead of letting a
        // second derivation exist. Secondary and Tertiary follow the accent rather than being
        // picked: a brand is two colours, and MudBlazor's own (a shocking pink) is what
        // reaches the screen if either is left unset.
        palette.Primary = new MudColor(theme.Accent);
        palette.PrimaryLighten = theme.AccentHover;
        palette.PrimaryDarken = theme.AccentPressed;
        palette.PrimaryContrastText = new MudColor(theme.TextOnAccent);
        palette.Secondary = new MudColor(theme.AccentFocus);
        palette.SecondaryContrastText = new MudColor(theme.TextOnAccent);
        palette.Tertiary = new MudColor(theme.AccentPressed);
        palette.TertiaryContrastText = new MudColor(theme.TextOnAccent);

        palette.Background = new MudColor(theme.Background);
        palette.Surface = new MudColor(theme.Surface);

        // MudBlazor has no slot for a raised surface; BackgroundGray is unused elsewhere
        // and carries it to the dialogs and popovers styled in ns-mud.css.
        palette.BackgroundGray = new MudColor(theme.SurfaceRaised);

        // The band is the brand's own colour and the content's surfaces are fixed, so the ink
        // on the chrome is derived from the chrome and never borrowed from the content: a light
        // chrome under a dark scheme would otherwise draw the scheme's white text on it.
        palette.AppbarBackground = new MudColor(theme.Chrome);
        palette.AppbarText = new MudColor(theme.TextOnChrome);
        palette.DrawerBackground = new MudColor(theme.Chrome);
        palette.DrawerText = new MudColor(theme.TextOnChrome);
        palette.DrawerIcon = new MudColor(theme.TextOnChromeSecondary);

        palette.TextPrimary = new MudColor(theme.TextPrimary);
        palette.TextSecondary = new MudColor(theme.TextSecondary);

        // Left at MudBlazor's own grey, icon buttons would sit a hue away from the brand.
        palette.ActionDefault = new MudColor(theme.TextSecondary);

        // The ink EVERY refused control is drawn in — a disabled button's word, a greyed menu
        // row, a disabled glyph — so it is the scheme's own third ink rung and never a state of
        // the accent: "no puedo tocar esto todavía" wearing the tenant's colour is
        // NsAs.Important's soft accent, a live act at low emphasis. Both vendor slots take it:
        // the vendor styles a refused control from either one depending on the control, and a
        // rung that is the ladder's third in one slot and the vendor's own translucent grey in
        // the other is two refusals on one screen.
        palette.ActionDisabled = new MudColor(theme.TextDisabled);
        palette.TextDisabled = new MudColor(theme.TextDisabled);

        palette.LinesDefault = new MudColor(theme.Lines);
        palette.TableLines = new MudColor(theme.Lines);
        palette.Divider = new MudColor(theme.Lines);

        // TableStriped is left at the vendor's own default and never reached: no table in the
        // house carries Striped, so the slot paints nothing. A value here would be a tint the
        // theme owns with nowhere to land.
        palette.TableHover = new MudColor(theme.RowHover);

        palette.Error = new MudColor(theme.Error);
        palette.Warning = new MudColor(theme.Warning);
        palette.Success = new MudColor(theme.Success);
        palette.Info = new MudColor(theme.Info);

        // The ink a FILLED face of each severity is read in — an alert, a toast, a filled chip.
        // Left unset, the vendor draws every one of them in its own white, which on the dark
        // scheme's own Info is 2.21:1: a block of colour with a ghost of text on it. The ink
        // belongs to the fill and not to the scheme's surfaces, so it comes from the palette's
        // own per-severity constants rather than from the ink ladder.
        palette.ErrorContrastText = new MudColor(theme.TextOnError);
        palette.WarningContrastText = new MudColor(theme.TextOnWarning);
        palette.SuccessContrastText = new MudColor(theme.TextOnSuccess);
        palette.InfoContrastText = new MudColor(theme.TextOnInfo);

        return palette;
    }

    /// <summary>MudBlazor's 26 elevation levels (0 = none), retuned for a dark canvas.</summary>
    // Material's shadows are black at 12-20%, which is invisible on a dark surface. Each
    // level here casts further and darker, and carries a translucent white ring: in dark
    // it is the lit top edge that reads as "lifted", and in light it falls away to nothing
    // over a white surface, leaving the shadow to do the work.
    static string[] BuildElevations()
    {
        var elevations = new string[26];

        elevations[0] = "none";

        for (var level = 1; level < elevations.Length; level++)
        {
            var offset = 1 + level;
            var blur = 2 + (level * 2.4);
            var alpha = Math.Min(0.45, 0.22 + (level * 0.01));

            elevations[level] = FormattableString.Invariant(
                $"0 {offset}px {blur:0.#}px rgba(0,0,0,{alpha:0.##}), 0 0 0 1px rgba(255,255,255,0.06)");
        }

        return elevations;
    }
}
